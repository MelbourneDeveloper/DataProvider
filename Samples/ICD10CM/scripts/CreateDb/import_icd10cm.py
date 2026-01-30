#!/usr/bin/env python3
"""
ICD-10-CM Import Script (US Clinical Modification)

Imports ICD-10-CM diagnosis codes from CDC XML Tabular file (FREE, Public Domain).
Includes full clinical details: inclusions, exclusions, code first, code also.

Source: https://ftp.cdc.gov/pub/Health_Statistics/NCHS/Publications/ICD10CM/

Usage:
    python import_icd10cm.py --db-path ./icd10cm.db
"""

import logging
import sqlite3
import uuid
import zipfile
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import datetime
from io import BytesIO
from typing import Generator

import click
import requests
from tqdm import tqdm

logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

CDC_XML_URL = "https://ftp.cdc.gov/pub/Health_Statistics/NCHS/Publications/ICD10CM/2025/icd10cm-table-index-2025.zip"
XML_FILENAME = "icd-10-cm-tabular-2025.xml"


@dataclass(frozen=True)
class Code:
    id: str
    code: str
    short_description: str
    long_description: str
    inclusion_terms: str
    exclusion_terms: str
    code_also: str
    code_first: str
    billable: bool


def generate_uuid() -> str:
    return str(uuid.uuid4())


def get_timestamp() -> str:
    return datetime.utcnow().isoformat() + "Z"


def extract_notes(element: ET.Element, tag: str) -> list[str]:
    """Extract all note texts from a specific child element."""
    notes = []
    child = element.find(tag)
    if child is not None:
        for note in child.findall("note"):
            if note.text:
                notes.append(note.text.strip())
    return notes


def extract_all_notes(element: ET.Element, tags: list[str]) -> str:
    """Extract and combine notes from multiple tags."""
    all_notes = []
    for tag in tags:
        all_notes.extend(extract_notes(element, tag))
    return "; ".join(all_notes) if all_notes else ""


def parse_diag(diag: ET.Element, parent_includes: str = "", parent_excludes: str = "") -> Generator[Code, None, None]:
    """Recursively parse a diag element and its children."""
    name_elem = diag.find("name")
    desc_elem = diag.find("desc")

    if name_elem is None or name_elem.text is None:
        return

    code = name_elem.text.strip()
    desc = desc_elem.text.strip() if desc_elem is not None and desc_elem.text else ""

    # Extract clinical details
    inclusion = extract_all_notes(diag, ["inclusionTerm", "includes"])
    exclusion = extract_all_notes(diag, ["excludes1", "excludes2"])
    code_also = extract_all_notes(diag, ["codeAlso", "useAdditionalCode"])
    code_first = extract_all_notes(diag, ["codeFirst"])

    # Inherit parent includes/excludes if this code has none
    if not inclusion and parent_includes:
        inclusion = parent_includes
    if not exclusion and parent_excludes:
        exclusion = parent_excludes

    # Check if billable (has no child diag elements with actual codes)
    child_diags = [d for d in diag.findall("diag") if d.find("name") is not None]
    billable = len(child_diags) == 0

    yield Code(
        id=generate_uuid(),
        code=code,
        short_description=desc[:100] if desc else "",
        long_description=desc,
        inclusion_terms=inclusion,
        exclusion_terms=exclusion,
        code_also=code_also,
        code_first=code_first,
        billable=billable,
    )

    # Recursively process child diag elements
    for child_diag in child_diags:
        yield from parse_diag(child_diag, inclusion, exclusion)


def download_and_parse() -> Generator[Code, None, None]:
    """Download CDC XML and parse all codes."""
    logger.info("Downloading ICD-10-CM XML from CDC...")
    response = requests.get(CDC_XML_URL, timeout=120)
    if response.status_code != 200:
        raise Exception(f"CDC download failed: HTTP {response.status_code}")
    logger.info("Downloaded CDC ICD-10-CM 2025 XML Tabular")

    # Extract XML from zip
    with zipfile.ZipFile(BytesIO(response.content)) as zf:
        logger.info(f"Extracting {XML_FILENAME}...")
        with zf.open(XML_FILENAME) as f:
            tree = ET.parse(f)

    root = tree.getroot()

    # Process all chapters
    for chapter in root.findall(".//chapter"):
        chapter_name = chapter.find("name")
        if chapter_name is not None:
            logger.info(f"Processing Chapter {chapter_name.text}...")

        # Process all sections in chapter
        for section in chapter.findall(".//section"):
            # Process all diag elements in section
            for diag in section.findall("diag"):
                yield from parse_diag(diag)


class SQLiteImporter:
    """Imports into existing Migration-created schema."""

    def __init__(self, db_path: str):
        self.conn = sqlite3.connect(db_path)

    def import_codes(self, codes: list[Code]):
        """Import codes into icd10cm_code table."""
        logger.info(f"Importing {len(codes)} codes into icd10cm_code")

        # Clear existing codes
        self.conn.execute("DELETE FROM icd10cm_code_embedding")
        self.conn.execute("DELETE FROM icd10cm_code")

        for c in codes:
            self.conn.execute(
                """INSERT INTO icd10cm_code
                   (Id, CategoryId, Code, ShortDescription, LongDescription,
                    InclusionTerms, ExclusionTerms, CodeAlso, CodeFirst,
                    Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated, VersionId)
                   VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
                (
                    c.id,
                    "",
                    c.code,
                    c.short_description,
                    c.long_description,
                    c.inclusion_terms,
                    c.exclusion_terms,
                    c.code_also,
                    c.code_first,
                    1 if c.billable else 0,
                    "2024-10-01",
                    "",
                    2025,
                    get_timestamp(),
                    1,
                ),
            )
        self.conn.commit()

    def close(self):
        self.conn.close()


@click.command()
@click.option("--db-path", default="icd10cm.db")
def main(db_path: str):
    logger.info("=" * 60)
    logger.info("ICD-10-CM Import (CDC XML Tabular - Full Clinical Details)")
    logger.info("=" * 60)

    codes = list(tqdm(download_and_parse(), desc="Parsing XML"))
    logger.info(f"Total: {len(codes)} codes parsed from XML")

    # Stats
    with_inclusions = sum(1 for c in codes if c.inclusion_terms)
    with_exclusions = sum(1 for c in codes if c.exclusion_terms)
    with_code_also = sum(1 for c in codes if c.code_also)
    with_code_first = sum(1 for c in codes if c.code_first)
    billable = sum(1 for c in codes if c.billable)

    logger.info(f"  With inclusions: {with_inclusions}")
    logger.info(f"  With exclusions: {with_exclusions}")
    logger.info(f"  With code also: {with_code_also}")
    logger.info(f"  With code first: {with_code_first}")
    logger.info(f"  Billable codes: {billable}")

    importer = SQLiteImporter(db_path)
    try:
        importer.import_codes(codes)
        logger.info(f"SUCCESS! {len(codes)} ICD-10-CM codes imported to {db_path}")
    finally:
        importer.close()


if __name__ == "__main__":
    main()

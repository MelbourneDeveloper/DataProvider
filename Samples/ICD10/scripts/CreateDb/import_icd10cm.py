#!/usr/bin/env python3
"""
ICD-10-CM Import Script (US Clinical Modification)

Imports ICD-10-CM diagnosis codes from CDC XML files (FREE, Public Domain).
Includes full clinical details: inclusions, exclusions, code first, code also, SYNONYMS.
Also imports the FULL HIERARCHY: chapters, blocks, and categories.

Source: https://ftp.cdc.gov/pub/Health_Statistics/NCHS/Publications/ICD10CM/

Usage:
    python import_icd10cm.py --db-path ./icd10cm.db
"""

import logging
import sqlite3
import uuid
import zipfile
import xml.etree.ElementTree as ET
from collections import defaultdict
from dataclasses import dataclass
from datetime import datetime
from io import BytesIO
from typing import Generator, Union

import click
import requests

logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

CDC_XML_URL = "https://ftp.cdc.gov/pub/Health_Statistics/NCHS/Publications/ICD10CM/2025/icd10cm-table-index-2025.zip"
TABULAR_XML = "icd-10-cm-tabular-2025.xml"
INDEX_XML = "icd-10-cm-index-2025.xml"

# ACHI data source - Australian Government (simplified sample for testing)
# Real ACHI data requires license from IHPA
ACHI_SAMPLE_BLOCKS = [
    ("achi-blk-1", "1820", "Coronary artery procedures", "38400-00", "38599-00"),
    ("achi-blk-2", "1821", "Heart valve procedures", "38600-00", "38699-00"),
    ("achi-blk-3", "1822", "Cardiac pacemaker procedures", "38700-00", "38799-00"),
]

ACHI_SAMPLE_CODES = [
    ("achi-code-1", "achi-blk-1", "38497-00", "Coronary angiography", "Coronary angiography with contrast"),
    ("achi-code-2", "achi-blk-1", "38500-00", "Coronary artery bypass", "Coronary artery bypass graft, single vessel"),
    ("achi-code-3", "achi-blk-1", "38503-00", "Coronary artery bypass, multiple", "Coronary artery bypass graft, multiple vessels"),
    ("achi-code-4", "achi-blk-2", "38600-00", "Aortic valve replacement", "Aortic valve replacement, open"),
    ("achi-code-5", "achi-blk-2", "38612-00", "Mitral valve repair", "Mitral valve repair, open"),
    ("achi-code-6", "achi-blk-3", "38700-00", "Pacemaker insertion", "Insertion of permanent pacemaker"),
]


@dataclass
class Chapter:
    id: str
    chapter_number: str
    title: str
    code_range_start: str
    code_range_end: str


@dataclass
class Block:
    id: str
    chapter_id: str
    block_code: str
    title: str
    code_range_start: str
    code_range_end: str


@dataclass
class Category:
    id: str
    block_id: str
    category_code: str
    title: str


@dataclass
class Code:
    id: str
    category_id: str
    code: str
    short_description: str
    long_description: str
    inclusion_terms: str
    exclusion_terms: str
    code_also: str
    code_first: str
    synonyms: str
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


def parse_index_for_synonyms(index_root: ET.Element) -> dict[str, list[str]]:
    """Parse the Index XML to build code -> synonyms mapping."""
    logger.info("Parsing Index XML for synonyms...")
    synonyms: dict[str, list[str]] = defaultdict(list)

    def process_term(term: ET.Element, prefix: str = "") -> None:
        """Recursively process term elements."""
        title_elem = term.find("title")
        code_elem = term.find("code")

        if title_elem is not None and title_elem.text:
            title = title_elem.text.strip()
            # Include any nemod (non-essential modifier) text
            nemod = term.find(".//nemod")
            if nemod is not None and nemod.text:
                title = f"{title} {nemod.text.strip()}"

            full_title = f"{prefix} {title}".strip() if prefix else title

            if code_elem is not None and code_elem.text:
                code = code_elem.text.strip().upper()
                if full_title and len(full_title) > 2:
                    synonyms[code].append(full_title)

        # Process nested terms
        for child_term in term.findall("term"):
            child_title = ""
            child_title_elem = term.find("title")
            if child_title_elem is not None and child_title_elem.text:
                child_title = child_title_elem.text.strip()
            process_term(child_term, child_title)

    # Process all mainTerm elements
    for letter in index_root.findall(".//letter"):
        for main_term in letter.findall("mainTerm"):
            process_term(main_term)

    logger.info(f"Found synonyms for {len(synonyms)} codes")
    return dict(synonyms)


def roman_to_int(roman: str) -> int:
    """Convert Roman numeral to integer."""
    values = {'I': 1, 'V': 5, 'X': 10, 'L': 50, 'C': 100, 'D': 500, 'M': 1000}
    result = 0
    prev = 0
    for char in reversed(roman.upper()):
        curr = values.get(char, 0)
        if curr < prev:
            result -= curr
        else:
            result += curr
        prev = curr
    return result


def parse_chapter_range(desc: str) -> tuple[str, str]:
    """Extract code range from chapter description like '(A00-B99)'."""
    import re
    match = re.search(r'\(([A-Z]\d+)-([A-Z]\d+)\)', desc)
    if match:
        return match.group(1), match.group(2)
    return "", ""


def download_and_parse() -> tuple[list[Chapter], list[Block], list[Category], list[Code], dict[str, list[str]]]:
    """Download CDC XML and parse all codes plus synonyms."""
    logger.info("Downloading ICD-10-CM XML from CDC...")
    response = requests.get(CDC_XML_URL, timeout=120)
    if response.status_code != 200:
        raise Exception(f"CDC download failed: HTTP {response.status_code}")
    logger.info("Downloaded CDC ICD-10-CM 2025 XML files")

    chapters = []
    blocks = []
    categories = []
    codes = []
    synonyms = {}

    # Track mappings for linking
    chapter_map = {}  # chapter_number -> chapter_id
    block_map = {}    # block_code -> block_id
    category_map = {} # category_code -> category_id

    with zipfile.ZipFile(BytesIO(response.content)) as zf:
        # Parse Tabular XML for codes
        logger.info(f"Extracting {TABULAR_XML}...")
        with zf.open(TABULAR_XML) as f:
            tabular_tree = ET.parse(f)

        tabular_root = tabular_tree.getroot()

        for chapter_elem in tabular_root.findall(".//chapter"):
            chapter_name_elem = chapter_elem.find("name")
            chapter_desc_elem = chapter_elem.find("desc")

            if chapter_name_elem is None or chapter_name_elem.text is None:
                continue

            chapter_number = chapter_name_elem.text.strip()
            chapter_title = chapter_desc_elem.text.strip() if chapter_desc_elem is not None and chapter_desc_elem.text else ""

            # Parse code range from chapter description or sectionIndex
            code_range_start, code_range_end = parse_chapter_range(chapter_title)

            # If not in title, try to get from first/last section
            if not code_range_start:
                sections = chapter_elem.findall(".//sectionRef")
                if sections:
                    first_id = sections[0].get("id", "")
                    last_id = sections[-1].get("id", "")
                    if "-" in first_id:
                        code_range_start = first_id.split("-")[0]
                    if "-" in last_id:
                        code_range_end = last_id.split("-")[1]

            chapter_id = generate_uuid()
            chapter_map[chapter_number] = chapter_id

            chapters.append(Chapter(
                id=chapter_id,
                chapter_number=chapter_number,
                title=chapter_title,
                code_range_start=code_range_start,
                code_range_end=code_range_end,
            ))

            logger.info(f"Processing Chapter {chapter_number}: {chapter_title[:50]}...")

            # Process sections (blocks)
            for section_elem in chapter_elem.findall(".//section"):
                section_id_attr = section_elem.get("id", "")
                section_desc_elem = section_elem.find("desc")

                if not section_id_attr:
                    continue

                block_code = section_id_attr  # e.g., "A00-A09"
                block_title = section_desc_elem.text.strip() if section_desc_elem is not None and section_desc_elem.text else ""

                # Parse code range from block code
                if "-" in block_code:
                    parts = block_code.split("-")
                    block_start, block_end = parts[0], parts[1]
                else:
                    block_start, block_end = block_code, block_code

                block_id = generate_uuid()
                block_map[block_code] = block_id

                blocks.append(Block(
                    id=block_id,
                    chapter_id=chapter_id,
                    block_code=block_code,
                    title=block_title,
                    code_range_start=block_start,
                    code_range_end=block_end,
                ))

                # Process diag elements (categories and codes)
                for diag in section_elem.findall("diag"):
                    parsed = list(parse_diag_with_hierarchy(diag, block_id, category_map))
                    for item in parsed:
                        if isinstance(item, Category):
                            categories.append(item)
                        elif isinstance(item, Code):
                            codes.append(item)

        # Parse Index XML for synonyms
        logger.info(f"Extracting {INDEX_XML}...")
        with zf.open(INDEX_XML) as f:
            index_tree = ET.parse(f)

        synonyms = parse_index_for_synonyms(index_tree.getroot())

    return chapters, blocks, categories, codes, synonyms


def parse_diag_with_hierarchy(
    diag: ET.Element,
    block_id: str,
    category_map: dict[str, str],
    parent_category_id: str = "",
    parent_includes: str = "",
    parent_excludes: str = "",
    depth: int = 0
) -> Generator[Union[Category, Code], None, None]:
    """Recursively parse a diag element, creating categories and codes."""
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

    # Check if this is a category (3-char code) or a full code
    child_diags = [d for d in diag.findall("diag") if d.find("name") is not None]
    is_category = len(code) == 3 and len(child_diags) > 0

    current_category_id = parent_category_id

    if is_category:
        # This is a category (e.g., A00, B01)
        category_id = generate_uuid()
        category_map[code] = category_id
        current_category_id = category_id

        yield Category(
            id=category_id,
            block_id=block_id,
            category_code=code,
            title=desc,
        )

    # If no children, this is a billable code
    if len(child_diags) == 0:
        # Determine category_id - for 3-char codes without children, they are their own category
        if len(code) == 3:
            category_id = generate_uuid()
            category_map[code] = category_id
            current_category_id = category_id

            yield Category(
                id=category_id,
                block_id=block_id,
                category_code=code,
                title=desc,
            )
        elif not current_category_id and len(code) >= 3:
            # Try to find category from code prefix
            cat_code = code[:3]
            current_category_id = category_map.get(cat_code, "")

        yield Code(
            id=generate_uuid(),
            category_id=current_category_id,
            code=code,
            short_description=desc[:100] if desc else "",
            long_description=desc,
            inclusion_terms=inclusion,
            exclusion_terms=exclusion,
            code_also=code_also,
            code_first=code_first,
            synonyms="",  # Will be filled later
            billable=True,
        )
    else:
        # Has children - if it's not a 3-char category, still create a code entry (non-billable)
        if not is_category:
            if not current_category_id and len(code) >= 3:
                cat_code = code[:3]
                current_category_id = category_map.get(cat_code, "")

            yield Code(
                id=generate_uuid(),
                category_id=current_category_id,
                code=code,
                short_description=desc[:100] if desc else "",
                long_description=desc,
                inclusion_terms=inclusion,
                exclusion_terms=exclusion,
                code_also=code_also,
                code_first=code_first,
                synonyms="",
                billable=False,
            )

    # Recursively process child diag elements
    for child_diag in child_diags:
        yield from parse_diag_with_hierarchy(
            child_diag, block_id, category_map, current_category_id, inclusion, exclusion, depth + 1
        )


class SQLiteImporter:
    """Imports into fresh Migration-created schema (import.sh deletes DB first)."""

    def __init__(self, db_path: str):
        self.conn = sqlite3.connect(db_path)

    def import_chapters(self, chapters: list[Chapter]):
        """Import chapters into icd10_chapter table."""
        logger.info(f"Importing {len(chapters)} chapters into icd10_chapter")

        for c in chapters:
            self.conn.execute(
                """INSERT INTO icd10_chapter
                   (Id, ChapterNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
                   VALUES (?,?,?,?,?,?,?)""",
                (c.id, c.chapter_number, c.title, c.code_range_start, c.code_range_end, get_timestamp(), 1),
            )
        self.conn.commit()

    def import_blocks(self, blocks: list[Block]):
        """Import blocks into icd10_block table."""
        logger.info(f"Importing {len(blocks)} blocks into icd10_block")

        for b in blocks:
            self.conn.execute(
                """INSERT INTO icd10_block
                   (Id, ChapterId, BlockCode, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
                   VALUES (?,?,?,?,?,?,?,?)""",
                (b.id, b.chapter_id, b.block_code, b.title, b.code_range_start, b.code_range_end, get_timestamp(), 1),
            )
        self.conn.commit()

    def import_categories(self, categories: list[Category]):
        """Import categories into icd10_category table."""
        logger.info(f"Importing {len(categories)} categories into icd10_category")
        self.conn.execute("DELETE FROM icd10_category")

        for c in categories:
            self.conn.execute(
                """INSERT INTO icd10_category
                   (Id, BlockId, CategoryCode, Title, LastUpdated, VersionId)
                   VALUES (?,?,?,?,?,?)""",
                (c.id, c.block_id, c.category_code, c.title, get_timestamp(), 1),
            )
        self.conn.commit()

    def import_codes(self, codes: list[Code], synonyms: dict[str, list[str]]):
        """Import codes into icd10_code table with synonyms."""
        logger.info(f"Importing {len(codes)} codes into icd10_code")

        for c in codes:
            # Get synonyms for this code
            code_synonyms = synonyms.get(c.code.upper(), [])
            # Deduplicate and remove the description itself
            unique_synonyms = list(set(
                s for s in code_synonyms
                if s.lower() != c.short_description.lower()
                and s.lower() != c.long_description.lower()
            ))
            synonyms_str = "; ".join(unique_synonyms[:20])  # Limit to 20 synonyms

            self.conn.execute(
                """INSERT INTO icd10_code
                   (Id, CategoryId, Code, ShortDescription, LongDescription,
                    InclusionTerms, ExclusionTerms, CodeAlso, CodeFirst, Synonyms,
                    Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated, VersionId)
                   VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
                (
                    c.id,
                    c.category_id,
                    c.code,
                    c.short_description,
                    c.long_description,
                    c.inclusion_terms,
                    c.exclusion_terms,
                    c.code_also,
                    c.code_first,
                    synonyms_str,
                    1 if c.billable else 0,
                    "2024-10-01",
                    "",
                    "2025",
                    get_timestamp(),
                    1,
                ),
            )
        self.conn.commit()

    def import_achi_sample_data(self):
        """Import sample ACHI data for testing."""
        logger.info("Importing sample ACHI data for testing...")

        # Import blocks
        for block_id, block_num, title, start, end in ACHI_SAMPLE_BLOCKS:
            self.conn.execute(
                """INSERT INTO achi_block
                   (Id, BlockNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
                   VALUES (?,?,?,?,?,?,?)""",
                (block_id, block_num, title, start, end, get_timestamp(), 1),
            )

        # Import codes
        for code_id, block_id, code, short_desc, long_desc in ACHI_SAMPLE_CODES:
            self.conn.execute(
                """INSERT INTO achi_code
                   (Id, BlockId, Code, ShortDescription, LongDescription, Billable,
                    EffectiveFrom, EffectiveTo, Edition, LastUpdated, VersionId)
                   VALUES (?,?,?,?,?,?,?,?,?,?,?)""",
                (code_id, block_id, code, short_desc, long_desc, 1, "2024-07-01", "", "13", get_timestamp(), 1),
            )

        self.conn.commit()
        logger.info(f"Imported {len(ACHI_SAMPLE_BLOCKS)} ACHI blocks and {len(ACHI_SAMPLE_CODES)} ACHI codes")

    def close(self):
        self.conn.close()


@click.command()
@click.option("--db-path", default="icd10cm.db")
def main(db_path: str):
    logger.info("=" * 60)
    logger.info("ICD-10-CM Import (CDC XML - Full Hierarchy + Synonyms)")
    logger.info("=" * 60)

    chapters, blocks, categories, codes, synonyms = download_and_parse()

    logger.info(f"Total: {len(chapters)} chapters parsed")
    logger.info(f"Total: {len(blocks)} blocks parsed")
    logger.info(f"Total: {len(categories)} categories parsed")
    logger.info(f"Total: {len(codes)} codes parsed from Tabular XML")
    logger.info(f"Total: {len(synonyms)} codes have synonyms from Index XML")

    # Stats
    with_inclusions = sum(1 for c in codes if c.inclusion_terms)
    with_exclusions = sum(1 for c in codes if c.exclusion_terms)
    with_code_also = sum(1 for c in codes if c.code_also)
    with_code_first = sum(1 for c in codes if c.code_first)
    billable = sum(1 for c in codes if c.billable)
    codes_with_synonyms = sum(1 for c in codes if c.code.upper() in synonyms)

    logger.info(f"  With inclusions: {with_inclusions}")
    logger.info(f"  With exclusions: {with_exclusions}")
    logger.info(f"  With code also: {with_code_also}")
    logger.info(f"  With code first: {with_code_first}")
    logger.info(f"  With synonyms: {codes_with_synonyms}")
    logger.info(f"  Billable codes: {billable}")

    importer = SQLiteImporter(db_path)
    try:
        importer.import_chapters(chapters)
        importer.import_blocks(blocks)
        importer.import_categories(categories)
        importer.import_codes(codes, synonyms)
        importer.import_achi_sample_data()
        logger.info(f"SUCCESS! Full ICD-10-CM hierarchy imported to {db_path}")
    finally:
        importer.close()


if __name__ == "__main__":
    main()

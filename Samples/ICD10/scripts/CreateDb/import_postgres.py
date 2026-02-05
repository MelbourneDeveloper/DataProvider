#!/usr/bin/env python3
"""
ICD-10-CM Import Script for PostgreSQL (Docker)

Imports ICD-10-CM diagnosis codes from CDC XML files into PostgreSQL.
"""

import logging
import os
import uuid
import zipfile
import xml.etree.ElementTree as ET
from collections import defaultdict
from dataclasses import dataclass
from datetime import datetime
from io import BytesIO
from typing import Generator, Union

import click
import psycopg2
import requests

logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

CDC_XML_URL = "https://ftp.cdc.gov/pub/Health_Statistics/NCHS/Publications/ICD10CM/2025/icd10cm-table-index-2025.zip"
TABULAR_XML = "icd-10-cm-tabular-2025.xml"
INDEX_XML = "icd-10-cm-index-2025.xml"

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
    notes = []
    child = element.find(tag)
    if child is not None:
        for note in child.findall("note"):
            if note.text:
                notes.append(note.text.strip())
    return notes


def extract_all_notes(element: ET.Element, tags: list[str]) -> str:
    all_notes = []
    for tag in tags:
        all_notes.extend(extract_notes(element, tag))
    return "; ".join(all_notes) if all_notes else ""


def parse_index_for_synonyms(index_root: ET.Element) -> dict[str, list[str]]:
    logger.info("Parsing Index XML for synonyms...")
    synonyms: dict[str, list[str]] = defaultdict(list)

    def process_term(term: ET.Element, prefix: str = "") -> None:
        title_elem = term.find("title")
        code_elem = term.find("code")

        if title_elem is not None and title_elem.text:
            title = title_elem.text.strip()
            nemod = term.find(".//nemod")
            if nemod is not None and nemod.text:
                title = f"{title} {nemod.text.strip()}"

            full_title = f"{prefix} {title}".strip() if prefix else title

            if code_elem is not None and code_elem.text:
                code = code_elem.text.strip().upper()
                if full_title and len(full_title) > 2:
                    synonyms[code].append(full_title)

        for child_term in term.findall("term"):
            child_title = ""
            child_title_elem = term.find("title")
            if child_title_elem is not None and child_title_elem.text:
                child_title = child_title_elem.text.strip()
            process_term(child_term, child_title)

    for letter in index_root.findall(".//letter"):
        for main_term in letter.findall("mainTerm"):
            process_term(main_term)

    logger.info(f"Found synonyms for {len(synonyms)} codes")
    return dict(synonyms)


def parse_chapter_range(desc: str) -> tuple[str, str]:
    import re
    match = re.search(r'\(([A-Z]\d+)-([A-Z]\d+)\)', desc)
    if match:
        return match.group(1), match.group(2)
    return "", ""


def download_and_parse() -> tuple[list[Chapter], list[Block], list[Category], list[Code], dict[str, list[str]]]:
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
    chapter_map = {}
    block_map = {}
    category_map = {}

    with zipfile.ZipFile(BytesIO(response.content)) as zf:
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
            code_range_start, code_range_end = parse_chapter_range(chapter_title)

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

            for section_elem in chapter_elem.findall(".//section"):
                section_id_attr = section_elem.get("id", "")
                section_desc_elem = section_elem.find("desc")

                if not section_id_attr:
                    continue

                block_code = section_id_attr
                block_title = section_desc_elem.text.strip() if section_desc_elem is not None and section_desc_elem.text else ""

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

                for diag in section_elem.findall("diag"):
                    parsed = list(parse_diag_with_hierarchy(diag, block_id, category_map))
                    for item in parsed:
                        if isinstance(item, Category):
                            categories.append(item)
                        elif isinstance(item, Code):
                            codes.append(item)

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
    name_elem = diag.find("name")
    desc_elem = diag.find("desc")

    if name_elem is None or name_elem.text is None:
        return

    code = name_elem.text.strip()
    desc = desc_elem.text.strip() if desc_elem is not None and desc_elem.text else ""

    inclusion = extract_all_notes(diag, ["inclusionTerm", "includes"])
    exclusion = extract_all_notes(diag, ["excludes1", "excludes2"])
    code_also = extract_all_notes(diag, ["codeAlso", "useAdditionalCode"])
    code_first = extract_all_notes(diag, ["codeFirst"])

    if not inclusion and parent_includes:
        inclusion = parent_includes
    if not exclusion and parent_excludes:
        exclusion = parent_excludes

    child_diags = [d for d in diag.findall("diag") if d.find("name") is not None]
    is_category = len(code) == 3 and len(child_diags) > 0

    current_category_id = parent_category_id

    if is_category:
        category_id = generate_uuid()
        category_map[code] = category_id
        current_category_id = category_id

        yield Category(
            id=category_id,
            block_id=block_id,
            category_code=code,
            title=desc,
        )

    if len(child_diags) == 0:
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
            billable=True,
        )
    else:
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

    for child_diag in child_diags:
        yield from parse_diag_with_hierarchy(
            child_diag, block_id, category_map, current_category_id, inclusion, exclusion, depth + 1
        )


class PostgresImporter:
    """Imports ICD-10 data into PostgreSQL."""

    def __init__(self, connection_string: str):
        self.conn = psycopg2.connect(connection_string)
        self.conn.autocommit = False

    def import_chapters(self, chapters: list[Chapter]):
        logger.info(f"Importing {len(chapters)} chapters")
        cur = self.conn.cursor()
        for c in chapters:
            cur.execute(
                """INSERT INTO "public"."icd10_chapter"
                   ("Id", "ChapterNumber", "Title", "CodeRangeStart", "CodeRangeEnd", "LastUpdated", "VersionId")
                   VALUES (%s,%s,%s,%s,%s,%s,%s)
                   ON CONFLICT ("Id") DO NOTHING""",
                (c.id, c.chapter_number, c.title, c.code_range_start, c.code_range_end, get_timestamp(), 1),
            )
        self.conn.commit()

    def import_blocks(self, blocks: list[Block]):
        logger.info(f"Importing {len(blocks)} blocks")
        cur = self.conn.cursor()
        for b in blocks:
            cur.execute(
                """INSERT INTO "public"."icd10_block"
                   ("Id", "ChapterId", "BlockCode", "Title", "CodeRangeStart", "CodeRangeEnd", "LastUpdated", "VersionId")
                   VALUES (%s,%s,%s,%s,%s,%s,%s,%s)
                   ON CONFLICT ("Id") DO NOTHING""",
                (b.id, b.chapter_id, b.block_code, b.title, b.code_range_start, b.code_range_end, get_timestamp(), 1),
            )
        self.conn.commit()

    def import_categories(self, categories: list[Category]):
        logger.info(f"Importing {len(categories)} categories")
        cur = self.conn.cursor()
        for c in categories:
            cur.execute(
                """INSERT INTO "public"."icd10_category"
                   ("Id", "BlockId", "CategoryCode", "Title", "LastUpdated", "VersionId")
                   VALUES (%s,%s,%s,%s,%s,%s)
                   ON CONFLICT ("Id") DO NOTHING""",
                (c.id, c.block_id, c.category_code, c.title, get_timestamp(), 1),
            )
        self.conn.commit()

    def import_codes(self, codes: list[Code], synonyms: dict[str, list[str]]):
        logger.info(f"Importing {len(codes)} codes")
        cur = self.conn.cursor()
        for c in codes:
            code_synonyms = synonyms.get(c.code.upper(), [])
            unique_synonyms = list(set(
                s for s in code_synonyms
                if s.lower() != c.short_description.lower()
                and s.lower() != c.long_description.lower()
            ))
            synonyms_str = "; ".join(unique_synonyms[:20])

            cur.execute(
                """INSERT INTO "public"."icd10_code"
                   ("Id", "CategoryId", "Code", "ShortDescription", "LongDescription",
                    "InclusionTerms", "ExclusionTerms", "CodeAlso", "CodeFirst", "Synonyms",
                    "Billable", "EffectiveFrom", "EffectiveTo", "Edition", "LastUpdated", "VersionId")
                   VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
                   ON CONFLICT ("Id") DO NOTHING""",
                (
                    c.id,
                    c.category_id if c.category_id else None,
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
        logger.info("Importing sample ACHI data...")
        cur = self.conn.cursor()

        for block_id, block_num, title, start, end in ACHI_SAMPLE_BLOCKS:
            cur.execute(
                """INSERT INTO "public"."achi_block"
                   ("Id", "BlockNumber", "Title", "CodeRangeStart", "CodeRangeEnd", "LastUpdated", "VersionId")
                   VALUES (%s,%s,%s,%s,%s,%s,%s)
                   ON CONFLICT ("Id") DO NOTHING""",
                (block_id, block_num, title, start, end, get_timestamp(), 1),
            )

        for code_id, block_id, code, short_desc, long_desc in ACHI_SAMPLE_CODES:
            cur.execute(
                """INSERT INTO "public"."achi_code"
                   ("Id", "BlockId", "Code", "ShortDescription", "LongDescription", "Billable",
                    "EffectiveFrom", "EffectiveTo", "Edition", "LastUpdated", "VersionId")
                   VALUES (%s,%s,%s,%s,%s,%s,%s,%s,%s,%s,%s)
                   ON CONFLICT ("Id") DO NOTHING""",
                (code_id, block_id, code, short_desc, long_desc, 1, "2024-07-01", "", "13", get_timestamp(), 1),
            )

        self.conn.commit()
        logger.info(f"Imported {len(ACHI_SAMPLE_BLOCKS)} ACHI blocks and {len(ACHI_SAMPLE_CODES)} ACHI codes")

    def close(self):
        self.conn.close()


@click.command()
@click.option("--connection-string", envvar="DATABASE_URL", required=True, help="PostgreSQL connection string")
def main(connection_string: str):
    logger.info("=" * 60)
    logger.info("ICD-10-CM Import for PostgreSQL")
    logger.info("=" * 60)

    chapters, blocks, categories, codes, synonyms = download_and_parse()

    logger.info(f"Total: {len(chapters)} chapters")
    logger.info(f"Total: {len(blocks)} blocks")
    logger.info(f"Total: {len(categories)} categories")
    logger.info(f"Total: {len(codes)} codes")

    importer = PostgresImporter(connection_string)
    try:
        importer.import_chapters(chapters)
        importer.import_blocks(blocks)
        importer.import_categories(categories)
        importer.import_codes(codes, synonyms)
        importer.import_achi_sample_data()
        logger.info("SUCCESS! ICD-10-CM imported to PostgreSQL")
    finally:
        importer.close()


if __name__ == "__main__":
    main()

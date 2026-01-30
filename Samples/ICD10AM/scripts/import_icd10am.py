#!/usr/bin/env python3
"""
ICD-10-AM Import Script (Australian FREE Data Sources)

Imports ICD-10-AM diagnosis codes from FREE Australian sources:
1. IHACPA EPD Short List (Emergency Principal Diagnosis) - FREE Excel download
2. WHO ICD-10 base classification (ICD-10-AM is based on this) - FREE

Data Sources (ALL FREE):
- EPD Short List: https://www.ihacpa.gov.au/health-care/classification/emergency-care/emergency-department-icd-10-am-principal-diagnosis-short-list
- WHO ICD-10: https://icdcdn.who.int/icd10/index.html

Usage:
    python import_icd10am.py --db-path ./icd10am.db
    python import_icd10am.py --db-path ./icd10am.db --include-who
"""

import json
import logging
import re
import sqlite3
import sys
import uuid
import zipfile
from dataclasses import dataclass
from datetime import datetime
from io import BytesIO
from pathlib import Path
from typing import Generator
from xml.etree import ElementTree as ET

import click
import requests
from tqdm import tqdm

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)

# FREE Australian ICD-10-AM data sources from IHACPA
# EPD Short List (Emergency Principal Diagnosis) - FREE from IHACPA
EPD_SHORTLIST_13_URL = "https://www.ihacpa.gov.au/sites/default/files/2025-05/epd_short_listicd-10-am_thirteenth_edition_distribution.xlsx"
EPD_SHORTLIST_11_URL = "https://www.ihacpa.gov.au/sites/default/files/2022-08/2_emergency_department_icd-10-am_eleventh_edition_principal_diagnosis_short_list_-_structural_hierarchies.xlsx"

# ICD-10-AM Electronic Appendices (contains full code lists) - FREE
ELECTRONIC_APPENDICES_12_URL = "https://www.ihacpa.gov.au/sites/default/files/2022-08/ICD-10-AM%20Twelfth%20Edition%20-%20electronic%20appendices.xlsx"
ELECTRONIC_APPENDICES_11_URL = "https://www.ihacpa.gov.au/sites/default/files/2022-08/icd-10-am_electronic_appendices_-_eleventh_edition.xlsx"

# Supplementary codes mapping
SUPPLEMENTARY_CODES_URL = "https://www.ihacpa.gov.au/sites/default/files/2022-08/supplementary_codes_for_chronic_conditions_map_to_icd-10-am_twelfth_edition.xlsx"

# CDC ICD-10-CM Fallback (US version, shares WHO ICD-10 base with ICD-10-AM)
# Public domain - US Government data
CDC_ICD10CM_2025_URL = "https://ftp.cdc.gov/pub/Health_Statistics/NCHS/Publications/ICD10CM/2025/ICD10-CM%20Code%20Descriptions%202025.zip"

EMBEDDING_MODEL = "abhinand5/medembed-small-v0.1"
BATCH_SIZE = 100


@dataclass(frozen=True)
class Chapter:
    id: str
    chapter_number: str
    title: str
    code_range_start: str
    code_range_end: str


@dataclass(frozen=True)
class Block:
    id: str
    chapter_id: str
    block_code: str
    title: str
    code_range_start: str
    code_range_end: str


@dataclass(frozen=True)
class Category:
    id: str
    block_id: str
    category_code: str
    title: str


@dataclass(frozen=True)
class Code:
    id: str
    category_id: str
    code: str
    short_description: str
    long_description: str
    billable: bool
    edition: int


def generate_uuid() -> str:
    return str(uuid.uuid4())


def get_timestamp() -> str:
    return datetime.utcnow().isoformat() + "Z"


# ICD-10 Chapter mappings (same for WHO base and Australian AM)
CHAPTER_MAP = {
    "A": ("I", "Certain infectious and parasitic diseases", "A00", "B99"),
    "B": ("I", "Certain infectious and parasitic diseases", "A00", "B99"),
    "C": ("II", "Neoplasms", "C00", "D48"),
    "D": ("II", "Neoplasms", "C00", "D48"),
    "E": ("IV", "Endocrine, nutritional and metabolic diseases", "E00", "E90"),
    "F": ("V", "Mental and behavioural disorders", "F00", "F99"),
    "G": ("VI", "Diseases of the nervous system", "G00", "G99"),
    "H": ("VII", "Diseases of the eye and adnexa", "H00", "H59"),
    "I": ("IX", "Diseases of the circulatory system", "I00", "I99"),
    "J": ("X", "Diseases of the respiratory system", "J00", "J99"),
    "K": ("XI", "Diseases of the digestive system", "K00", "K93"),
    "L": ("XII", "Diseases of the skin and subcutaneous tissue", "L00", "L99"),
    "M": ("XIII", "Diseases of the musculoskeletal system and connective tissue", "M00", "M99"),
    "N": ("XIV", "Diseases of the genitourinary system", "N00", "N99"),
    "O": ("XV", "Pregnancy, childbirth and the puerperium", "O00", "O99"),
    "P": ("XVI", "Certain conditions originating in the perinatal period", "P00", "P96"),
    "Q": ("XVII", "Congenital malformations, deformations and chromosomal abnormalities", "Q00", "Q99"),
    "R": ("XVIII", "Symptoms, signs and abnormal clinical and laboratory findings, not elsewhere classified", "R00", "R99"),
    "S": ("XIX", "Injury, poisoning and certain other consequences of external causes", "S00", "T98"),
    "T": ("XIX", "Injury, poisoning and certain other consequences of external causes", "S00", "T98"),
    "V": ("XX", "External causes of morbidity and mortality", "V01", "Y98"),
    "W": ("XX", "External causes of morbidity and mortality", "V01", "Y98"),
    "X": ("XX", "External causes of morbidity and mortality", "V01", "Y98"),
    "Y": ("XX", "External causes of morbidity and mortality", "V01", "Y98"),
    "Z": ("XXI", "Factors influencing health status and contact with health services", "Z00", "Z99"),
    "U": ("XXII", "Codes for special purposes", "U00", "U99"),
}


class ICD10AMDownloader:
    """Downloads ICD-10-AM data from FREE Australian sources."""

    def __init__(self):
        self.chapters: dict[str, Chapter] = {}
        self.blocks: dict[str, Block] = {}
        self.categories: dict[str, Category] = {}

    def download_epd_shortlist(self) -> Generator[Code, None, None]:
        """
        Download ICD-10-AM codes from IHACPA electronic appendices and EPD short lists.
        These are FREE downloads from the Australian government.
        """
        logger.info("Downloading ICD-10-AM from IHACPA (FREE)...")

        # Try multiple sources in order of preference
        urls_to_try = [
            (EPD_SHORTLIST_13_URL, "EPD Short List 13th Edition"),
            (EPD_SHORTLIST_11_URL, "EPD Short List 11th Edition (Hierarchies)"),
            (ELECTRONIC_APPENDICES_12_URL, "Electronic Appendices 12th Edition"),
            (ELECTRONIC_APPENDICES_11_URL, "Electronic Appendices 11th Edition"),
            (SUPPLEMENTARY_CODES_URL, "Supplementary Codes Map"),
        ]

        response = None
        for url, name in urls_to_try:
            try:
                logger.info(f"Trying {name}...")
                response = requests.get(url, timeout=60)
                if response.status_code == 200:
                    logger.info(f"SUCCESS: Downloaded {name}")
                    break
                logger.info(f"{name}: HTTP {response.status_code}")
            except Exception as e:
                logger.info(f"{name}: {e}")

        if response is None or response.status_code != 200:
            raise Exception("All IHACPA download URLs failed")

        # Parse Excel file
        try:
            import pandas as pd

            df = pd.read_excel(BytesIO(response.content), sheet_name=0)
            logger.info(f"Loaded Excel with {len(df)} rows, columns: {list(df.columns)}")

            # Find the code and description columns
            code_col = None
            desc_col = None
            for col in df.columns:
                col_lower = str(col).lower()
                if "code" in col_lower and code_col is None:
                    code_col = col
                elif ("description" in col_lower or "term" in col_lower or "label" in col_lower) and desc_col is None:
                    desc_col = col

            if code_col is None:
                # Try first column as code
                code_col = df.columns[0]
            if desc_col is None and len(df.columns) > 1:
                desc_col = df.columns[1]

            logger.info(f"Using columns: code={code_col}, description={desc_col}")

            for _, row in df.iterrows():
                code_str = str(row[code_col]).strip() if code_col else ""
                desc = str(row[desc_col]).strip() if desc_col else ""

                # Skip headers and empty rows
                if not code_str or code_str == "nan" or not re.match(r"^[A-Z]\d", code_str):
                    continue

                # Format code with decimal if needed
                if len(code_str) > 3 and "." not in code_str:
                    code_str = code_str[:3] + "." + code_str[3:]

                chapter_id = self._get_or_create_chapter(code_str)
                block_id = self._get_or_create_block(code_str, chapter_id)
                category_id = self._get_or_create_category(code_str, block_id, desc)

                yield Code(
                    id=generate_uuid(),
                    category_id=category_id,
                    code=code_str.upper(),
                    short_description=desc[:100],
                    long_description=desc,
                    billable=len(code_str.replace(".", "")) > 3,
                    edition=13,
                )

        except ImportError:
            logger.error("pandas and openpyxl required: pip install pandas openpyxl")
            raise

    def download_cdc_icd10cm(self) -> Generator[Code, None, None]:
        """
        Download ICD-10-CM codes from US CDC (FREE, Public Domain).
        ICD-10-CM shares the WHO ICD-10 base with ICD-10-AM.
        ~74,000 codes available.
        """
        logger.info("Downloading ICD-10-CM from CDC (FREE, Public Domain)...")

        try:
            logger.info(f"Trying CDC ICD-10-CM 2025...")
            response = requests.get(CDC_ICD10CM_2025_URL, timeout=120)
            if response.status_code != 200:
                raise Exception(f"CDC download failed: HTTP {response.status_code}")
            logger.info(f"SUCCESS: Downloaded CDC ICD-10-CM 2025")
        except Exception as e:
            logger.error(f"CDC download failed: {e}")
            raise

        # Extract zip file
        lines = []
        with zipfile.ZipFile(BytesIO(response.content)) as zf:
            for name in zf.namelist():
                if name.endswith(".txt"):
                    logger.info(f"Extracting {name}...")
                    with zf.open(name) as f:
                        content = f.read().decode("utf-8", errors="ignore")
                        lines.extend(content.strip().split("\n"))

        logger.info(f"Loaded {len(lines)} lines from CDC ICD-10-CM")

        for line in lines:
            line = line.strip()
            if not line:
                continue

            # CDC format: first 7 chars are code (padded), rest is description
            # Example: "A000    Cholera due to Vibrio cholerae 01, biovar cholerae"
            if len(line) > 7:
                code_str = line[:7].strip()
                desc = line[7:].strip()
            else:
                parts = line.split(None, 1)
                if len(parts) < 2:
                    continue
                code_str = parts[0].strip()
                desc = parts[1].strip() if len(parts) > 1 else ""

            # Skip non-ICD codes
            if not code_str or not re.match(r"^[A-Z]\d", code_str):
                continue

            # Format code with decimal
            if len(code_str) > 3 and "." not in code_str:
                code_str = code_str[:3] + "." + code_str[3:]

            chapter_id = self._get_or_create_chapter(code_str)
            block_id = self._get_or_create_block(code_str, chapter_id)
            category_id = self._get_or_create_category(code_str, block_id, desc)

            yield Code(
                id=generate_uuid(),
                category_id=category_id,
                code=code_str.upper(),
                short_description=desc[:100],
                long_description=desc,
                billable=len(code_str.replace(".", "")) > 3,
                edition=13,  # Mark as compatible with ICD-10-AM edition 13
            )

    def download_who_icd10(self) -> Generator[Code, None, None]:
        """
        Download WHO ICD-10 base classification (FREE).
        ICD-10-AM is based on this, so codes are compatible.
        """
        logger.info("Downloading WHO ICD-10 base classification (FREE)...")

        try:
            response = requests.get(WHO_ICD10_CLAML_URL, timeout=120)
            response.raise_for_status()

            with zipfile.ZipFile(BytesIO(response.content)) as zf:
                # Find the ClaML XML file
                xml_files = [f for f in zf.namelist() if f.endswith(".xml")]
                if not xml_files:
                    raise ValueError("No XML file found in WHO ICD-10 ZIP")

                logger.info(f"Parsing {xml_files[0]}...")
                with zf.open(xml_files[0]) as f:
                    yield from self._parse_claml(f.read())

        except Exception as e:
            logger.error(f"WHO ICD-10 download failed: {e}")
            raise

    def _parse_claml(self, xml_content: bytes) -> Generator[Code, None, None]:
        """Parse ClaML XML format (WHO ICD-10 standard)."""
        try:
            root = ET.fromstring(xml_content)

            # Find all Class elements (these are the codes)
            for cls in root.findall(".//{http://www.who.int/classifications/icd10}Class"):
                code = cls.get("code", "")
                kind = cls.get("kind", "")

                if not code or kind not in ("category", "block"):
                    continue

                # Get the preferred label (description)
                desc = ""
                for rubric in cls.findall("{http://www.who.int/classifications/icd10}Rubric"):
                    if rubric.get("kind") == "preferred":
                        label = rubric.find("{http://www.who.int/classifications/icd10}Label")
                        if label is not None and label.text:
                            desc = label.text
                            break

                if not desc:
                    continue

                # Format code
                if len(code) > 3 and "." not in code:
                    code = code[:3] + "." + code[3:]

                chapter_id = self._get_or_create_chapter(code)
                block_id = self._get_or_create_block(code, chapter_id)
                category_id = self._get_or_create_category(code, block_id, desc)

                yield Code(
                    id=generate_uuid(),
                    category_id=category_id,
                    code=code.upper(),
                    short_description=desc[:100],
                    long_description=desc,
                    billable=len(code.replace(".", "")) > 3,
                    edition=10,  # WHO ICD-10 base
                )

        except ET.ParseError:
            # Try without namespace
            root = ET.fromstring(xml_content)
            for cls in root.findall(".//Class"):
                code = cls.get("code", "")
                if not code:
                    continue

                desc = ""
                for rubric in cls.findall("Rubric"):
                    if rubric.get("kind") == "preferred":
                        label = rubric.find("Label")
                        if label is not None and label.text:
                            desc = label.text
                            break

                if not desc:
                    continue

                if len(code) > 3 and "." not in code:
                    code = code[:3] + "." + code[3:]

                chapter_id = self._get_or_create_chapter(code)
                block_id = self._get_or_create_block(code, chapter_id)
                category_id = self._get_or_create_category(code, block_id, desc)

                yield Code(
                    id=generate_uuid(),
                    category_id=category_id,
                    code=code.upper(),
                    short_description=desc[:100],
                    long_description=desc,
                    billable=len(code.replace(".", "")) > 3,
                    edition=10,
                )

    def _get_or_create_chapter(self, code: str) -> str:
        prefix = code[0].upper() if code else "Z"
        chapter_info = CHAPTER_MAP.get(prefix, ("XXI", "Unknown", "Z00", "Z99"))
        chapter_num = chapter_info[0]

        if chapter_num not in self.chapters:
            self.chapters[chapter_num] = Chapter(
                id=generate_uuid(),
                chapter_number=chapter_num,
                title=chapter_info[1],
                code_range_start=chapter_info[2],
                code_range_end=chapter_info[3],
            )
        return self.chapters[chapter_num].id

    def _get_or_create_block(self, code: str, chapter_id: str) -> str:
        block_code = code[:3].replace(".", "") if len(code) >= 3 else code
        if block_code not in self.blocks:
            self.blocks[block_code] = Block(
                id=generate_uuid(),
                chapter_id=chapter_id,
                block_code=block_code,
                title=f"Block {block_code}",
                code_range_start=block_code,
                code_range_end=block_code,
            )
        return self.blocks[block_code].id

    def _get_or_create_category(self, code: str, block_id: str, title: str = "") -> str:
        category_code = code[:3].replace(".", "") if len(code) >= 3 else code
        if category_code not in self.categories:
            self.categories[category_code] = Category(
                id=generate_uuid(),
                block_id=block_id,
                category_code=category_code,
                title=title[:100] if title else f"Category {category_code}",
            )
        return self.categories[category_code].id


class SQLiteImporter:
    """Imports ICD-10-AM data into SQLite."""

    def __init__(self, db_path: str):
        self.conn = sqlite3.connect(db_path)
        self._create_schema()

    def _create_schema(self):
        """Create database schema."""
        self.conn.executescript("""
            CREATE TABLE IF NOT EXISTS icd10am_chapter (
                Id TEXT PRIMARY KEY,
                ChapterNumber TEXT UNIQUE,
                Title TEXT,
                CodeRangeStart TEXT,
                CodeRangeEnd TEXT,
                LastUpdated TEXT,
                VersionId INTEGER DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS icd10am_block (
                Id TEXT PRIMARY KEY,
                ChapterId TEXT,
                BlockCode TEXT UNIQUE,
                Title TEXT,
                CodeRangeStart TEXT,
                CodeRangeEnd TEXT,
                LastUpdated TEXT,
                VersionId INTEGER DEFAULT 1,
                FOREIGN KEY (ChapterId) REFERENCES icd10am_chapter(Id)
            );

            CREATE TABLE IF NOT EXISTS icd10am_category (
                Id TEXT PRIMARY KEY,
                BlockId TEXT,
                CategoryCode TEXT UNIQUE,
                Title TEXT,
                LastUpdated TEXT,
                VersionId INTEGER DEFAULT 1,
                FOREIGN KEY (BlockId) REFERENCES icd10am_block(Id)
            );

            CREATE TABLE IF NOT EXISTS icd10am_code (
                Id TEXT PRIMARY KEY,
                CategoryId TEXT,
                Code TEXT UNIQUE,
                ShortDescription TEXT,
                LongDescription TEXT,
                InclusionTerms TEXT DEFAULT '',
                ExclusionTerms TEXT DEFAULT '',
                CodeAlso TEXT DEFAULT '',
                CodeFirst TEXT DEFAULT '',
                Billable INTEGER DEFAULT 1,
                EffectiveFrom TEXT DEFAULT '2025-07-01',
                EffectiveTo TEXT DEFAULT '',
                Edition INTEGER DEFAULT 13,
                LastUpdated TEXT,
                VersionId INTEGER DEFAULT 1,
                FOREIGN KEY (CategoryId) REFERENCES icd10am_category(Id)
            );

            CREATE TABLE IF NOT EXISTS icd10am_code_embedding (
                Id TEXT PRIMARY KEY,
                CodeId TEXT UNIQUE,
                Embedding TEXT,
                EmbeddingModel TEXT DEFAULT 'MedEmbed-Small-v0.1',
                LastUpdated TEXT,
                FOREIGN KEY (CodeId) REFERENCES icd10am_code(Id)
            );

            CREATE TABLE IF NOT EXISTS achi_block (
                Id TEXT PRIMARY KEY,
                BlockNumber TEXT UNIQUE,
                Title TEXT,
                CodeRangeStart TEXT,
                CodeRangeEnd TEXT,
                LastUpdated TEXT,
                VersionId INTEGER DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS achi_code (
                Id TEXT PRIMARY KEY,
                BlockId TEXT,
                Code TEXT UNIQUE,
                ShortDescription TEXT,
                LongDescription TEXT,
                Billable INTEGER DEFAULT 1,
                EffectiveFrom TEXT DEFAULT '2025-07-01',
                EffectiveTo TEXT DEFAULT '',
                Edition INTEGER DEFAULT 13,
                LastUpdated TEXT,
                VersionId INTEGER DEFAULT 1,
                FOREIGN KEY (BlockId) REFERENCES achi_block(Id)
            );

            CREATE TABLE IF NOT EXISTS achi_code_embedding (
                Id TEXT PRIMARY KEY,
                CodeId TEXT UNIQUE,
                Embedding TEXT,
                EmbeddingModel TEXT DEFAULT 'MedEmbed-Small-v0.1',
                LastUpdated TEXT,
                FOREIGN KEY (CodeId) REFERENCES achi_code(Id)
            );

            CREATE TABLE IF NOT EXISTS user_search_history (
                Id TEXT PRIMARY KEY,
                UserId TEXT,
                Query TEXT,
                SelectedCode TEXT,
                Timestamp TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_icd10am_code_code ON icd10am_code(Code);
            CREATE INDEX IF NOT EXISTS idx_icd10am_code_desc ON icd10am_code(ShortDescription);
        """)
        self.conn.commit()

    def import_chapters(self, chapters: list[Chapter]):
        logger.info(f"Importing {len(chapters)} chapters")
        cursor = self.conn.cursor()
        for c in chapters:
            cursor.execute(
                """
                INSERT OR REPLACE INTO icd10am_chapter
                (Id, ChapterNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated)
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (c.id, c.chapter_number, c.title, c.code_range_start, c.code_range_end, get_timestamp()),
            )
        self.conn.commit()

    def import_blocks(self, blocks: list[Block]):
        logger.info(f"Importing {len(blocks)} blocks")
        cursor = self.conn.cursor()
        for b in blocks:
            cursor.execute(
                """
                INSERT OR REPLACE INTO icd10am_block
                (Id, ChapterId, BlockCode, Title, CodeRangeStart, CodeRangeEnd, LastUpdated)
                VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (b.id, b.chapter_id, b.block_code, b.title, b.code_range_start, b.code_range_end, get_timestamp()),
            )
        self.conn.commit()

    def import_categories(self, categories: list[Category]):
        logger.info(f"Importing {len(categories)} categories")
        cursor = self.conn.cursor()
        for c in categories:
            cursor.execute(
                """
                INSERT OR REPLACE INTO icd10am_category
                (Id, BlockId, CategoryCode, Title, LastUpdated)
                VALUES (?, ?, ?, ?, ?)
                """,
                (c.id, c.block_id, c.category_code, c.title, get_timestamp()),
            )
        self.conn.commit()

    def import_codes(self, codes: list[Code]):
        logger.info(f"Importing {len(codes)} codes")
        cursor = self.conn.cursor()
        for c in codes:
            cursor.execute(
                """
                INSERT OR REPLACE INTO icd10am_code
                (Id, CategoryId, Code, ShortDescription, LongDescription, Billable, Edition, LastUpdated)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (c.id, c.category_id, c.code, c.short_description, c.long_description, 1 if c.billable else 0, c.edition, get_timestamp()),
            )
        self.conn.commit()

    def import_embeddings(self, embeddings: list[tuple[str, str, list[float]]]):
        logger.info(f"Importing {len(embeddings)} embeddings")
        cursor = self.conn.cursor()
        for code_id, code, embedding in embeddings:
            cursor.execute(
                """
                INSERT OR REPLACE INTO icd10am_code_embedding
                (Id, CodeId, Embedding, EmbeddingModel, LastUpdated)
                VALUES (?, ?, ?, ?, ?)
                """,
                (generate_uuid(), code_id, json.dumps(embedding), EMBEDDING_MODEL, get_timestamp()),
            )
        self.conn.commit()

    def close(self):
        self.conn.close()


@click.command()
@click.option("--db-path", type=click.Path(), default="icd10am.db", help="SQLite database path")
@click.option("--skip-embeddings", is_flag=True, help="Skip embedding generation")
@click.option("--include-who", is_flag=True, help="Include WHO ICD-10 base codes (adds ~12,000 more codes)")
def main(db_path: str, skip_embeddings: bool, include_who: bool):
    """
    Import ICD-10-AM codes from FREE Australian sources.

    Downloads:
    1. EPD Short List from IHACPA (FREE) - ~700 common emergency codes
    2. Optionally: WHO ICD-10 base (FREE) - ~12,000 codes (ICD-10-AM is based on this)

    No license required for these sources!
    """
    logger.info("=" * 60)
    logger.info("ICD-10-AM Import (FREE Australian Data Sources)")
    logger.info("=" * 60)

    downloader = ICD10AMDownloader()
    all_codes: dict[str, Code] = {}  # Use dict to dedupe by code

    # Download EPD Short List (try Australian source first)
    ihacpa_success = False
    try:
        for code in tqdm(downloader.download_epd_shortlist(), desc="EPD Short List"):
            all_codes[code.code] = code
        if len(all_codes) > 0:
            logger.info(f"Downloaded {len(all_codes)} codes from IHACPA EPD Short List")
            ihacpa_success = True
    except Exception as e:
        logger.warning(f"IHACPA EPD Short List failed: {e}")

    # Fallback to CDC ICD-10-CM if IHACPA failed
    if not ihacpa_success or len(all_codes) < 100:
        logger.info("IHACPA unavailable, using CDC ICD-10-CM as fallback...")
        logger.info("Note: ICD-10-CM shares WHO ICD-10 base with ICD-10-AM")
        try:
            cdc_count = 0
            for code in tqdm(downloader.download_cdc_icd10cm(), desc="CDC ICD-10-CM"):
                if code.code not in all_codes:
                    all_codes[code.code] = code
                    cdc_count += 1
            logger.info(f"Downloaded {cdc_count} codes from CDC ICD-10-CM")
        except Exception as e:
            logger.warning(f"CDC ICD-10-CM failed: {e}")

    # Optionally download WHO ICD-10 base
    if include_who:
        try:
            who_count = 0
            for code in tqdm(downloader.download_who_icd10(), desc="WHO ICD-10"):
                if code.code not in all_codes:
                    all_codes[code.code] = code
                    who_count += 1
            logger.info(f"Added {who_count} additional codes from WHO ICD-10")
        except Exception as e:
            logger.warning(f"WHO ICD-10 failed: {e}")

    codes = list(all_codes.values())
    logger.info(f"Total: {len(codes)} ICD-10-AM compatible codes")
    logger.info(f"Created {len(downloader.chapters)} chapters")
    logger.info(f"Created {len(downloader.blocks)} blocks")
    logger.info(f"Created {len(downloader.categories)} categories")

    # Import to SQLite
    importer = SQLiteImporter(db_path)

    try:
        importer.import_chapters(list(downloader.chapters.values()))
        importer.import_blocks(list(downloader.blocks.values()))
        importer.import_categories(list(downloader.categories.values()))
        importer.import_codes(codes)

        if not skip_embeddings:
            try:
                from sentence_transformers import SentenceTransformer

                logger.info(f"Loading embedding model: {EMBEDDING_MODEL}")
                model = SentenceTransformer(EMBEDDING_MODEL)

                embeddings = []
                for i in tqdm(range(0, len(codes), BATCH_SIZE), desc="Generating embeddings"):
                    batch = codes[i : i + BATCH_SIZE]
                    texts = [f"{c.code} {c.short_description}" for c in batch]
                    batch_embeddings = model.encode(texts, normalize_embeddings=True)
                    for code, emb in zip(batch, batch_embeddings):
                        embeddings.append((code.id, code.code, emb.tolist()))

                importer.import_embeddings(embeddings)

            except ImportError:
                logger.warning("sentence-transformers not installed. Skipping embeddings.")

        logger.info("=" * 60)
        logger.info(f"SUCCESS! Database created: {db_path}")
        logger.info(f"  - {len(codes)} ICD-10-AM codes")
        logger.info(f"  - {len(downloader.chapters)} chapters")
        logger.info("=" * 60)

    except Exception as e:
        logger.error(f"Import failed: {e}")
        sys.exit(1)
    finally:
        importer.close()


if __name__ == "__main__":
    main()

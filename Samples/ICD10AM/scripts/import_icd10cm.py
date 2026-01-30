#!/usr/bin/env python3
"""
ICD-10-CM Import Script (Open Source Data)

Imports ICD-10-CM diagnosis codes from CMS.gov (FREE, no license required!)
and ICD-10-PCS procedure codes into SQLite/PostgreSQL with vector embeddings.

Data Sources (ALL FREE):
- ICD-10-CM: https://www.cms.gov/medicare/coding-billing/icd-10-codes
- ICD-10-PCS: https://www.cms.gov/medicare/coding-billing/icd-10-codes

Usage:
    python import_icd10cm.py --db-path ./icd10.db
    python import_icd10cm.py --db-url postgresql://user:pass@localhost/icd10
"""

import json
import logging
import os
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

import click
import requests
from tqdm import tqdm

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)

# CMS.gov URLs for FREE ICD-10 data
CMS_ICD10CM_URL = "https://www.cms.gov/files/zip/2025-code-descriptions-tabular-order.zip"
CMS_ICD10PCS_URL = "https://www.cms.gov/files/zip/2025-icd-10-pcs-codes-file.zip"

# Alternative: GitHub mirror with JSON (faster, simpler)
GITHUB_ICD10_JSON_URL = "https://raw.githubusercontent.com/icd-codex/icd10-cm-hierarchy/main/icd10-codes-2024.json"
GITHUB_ICD10_GIST_URL = "https://gist.githubusercontent.com/cryocaustik/b86de96e66489ada97c25fc25f755de0/raw/icd10_codes.json"

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


# ICD-10 Chapter mappings (same for CM and AM - based on WHO ICD-10)
CHAPTER_MAP = {
    "A": ("I", "Certain infectious and parasitic diseases", "A00", "B99"),
    "B": ("I", "Certain infectious and parasitic diseases", "A00", "B99"),
    "C": ("II", "Neoplasms", "C00", "D49"),
    "D": ("II", "Neoplasms", "C00", "D49"),
    "E": ("IV", "Endocrine, nutritional and metabolic diseases", "E00", "E89"),
    "F": ("V", "Mental, Behavioral and Neurodevelopmental disorders", "F01", "F99"),
    "G": ("VI", "Diseases of the nervous system", "G00", "G99"),
    "H": ("VII", "Diseases of the eye and adnexa", "H00", "H59"),
    "I": ("IX", "Diseases of the circulatory system", "I00", "I99"),
    "J": ("X", "Diseases of the respiratory system", "J00", "J99"),
    "K": ("XI", "Diseases of the digestive system", "K00", "K95"),
    "L": ("XII", "Diseases of the skin and subcutaneous tissue", "L00", "L99"),
    "M": ("XIII", "Diseases of the musculoskeletal system and connective tissue", "M00", "M99"),
    "N": ("XIV", "Diseases of the genitourinary system", "N00", "N99"),
    "O": ("XV", "Pregnancy, childbirth and the puerperium", "O00", "O9A"),
    "P": ("XVI", "Certain conditions originating in the perinatal period", "P00", "P96"),
    "Q": ("XVII", "Congenital malformations, deformations and chromosomal abnormalities", "Q00", "Q99"),
    "R": ("XVIII", "Symptoms, signs and abnormal clinical and laboratory findings", "R00", "R99"),
    "S": ("XIX", "Injury, poisoning and certain other consequences of external causes", "S00", "T88"),
    "T": ("XIX", "Injury, poisoning and certain other consequences of external causes", "S00", "T88"),
    "V": ("XX", "External causes of morbidity", "V00", "Y99"),
    "W": ("XX", "External causes of morbidity", "V00", "Y99"),
    "X": ("XX", "External causes of morbidity", "V00", "Y99"),
    "Y": ("XX", "External causes of morbidity", "V00", "Y99"),
    "Z": ("XXI", "Factors influencing health status and contact with health services", "Z00", "Z99"),
}


class ICD10CMDownloader:
    """Downloads ICD-10-CM data from CMS.gov (FREE!)."""

    def __init__(self):
        self.chapters: dict[str, Chapter] = {}
        self.blocks: dict[str, Block] = {}
        self.categories: dict[str, Category] = {}

    def download_from_cms(self) -> Generator[Code, None, None]:
        """Download and parse ICD-10-CM from CMS.gov ZIP file."""
        logger.info("Downloading ICD-10-CM from CMS.gov...")

        try:
            response = requests.get(CMS_ICD10CM_URL, timeout=120)
            response.raise_for_status()

            with zipfile.ZipFile(BytesIO(response.content)) as zf:
                # Find the code descriptions file (usually .txt)
                txt_files = [f for f in zf.namelist() if f.endswith(".txt")]
                if not txt_files:
                    raise ValueError("No .txt file found in ZIP")

                # Parse the tabular order file
                for txt_file in txt_files:
                    if "order" in txt_file.lower() or "desc" in txt_file.lower():
                        logger.info(f"Parsing {txt_file}...")
                        with zf.open(txt_file) as f:
                            yield from self._parse_cms_txt(f.read().decode("utf-8", errors="ignore"))
                        return

                # Fallback: try first .txt file
                logger.info(f"Parsing {txt_files[0]}...")
                with zf.open(txt_files[0]) as f:
                    yield from self._parse_cms_txt(f.read().decode("utf-8", errors="ignore"))

        except Exception as e:
            logger.warning(f"CMS download failed: {e}. Falling back to GitHub mirror...")
            yield from self.download_from_github()

    def download_from_github(self) -> Generator[Code, None, None]:
        """Download ICD-10-CM from GitHub JSON mirror (simpler, faster)."""
        logger.info("Downloading ICD-10-CM from GitHub mirror...")

        try:
            response = requests.get(GITHUB_ICD10_GIST_URL, timeout=60)
            response.raise_for_status()
            data = response.json()

            for item in data:
                code_str = item.get("code", "").strip()
                desc = item.get("desc", "").strip()
                if not code_str or not desc:
                    continue

                # Format code with decimal point if needed
                if len(code_str) > 3 and "." not in code_str:
                    code_str = code_str[:3] + "." + code_str[3:]

                chapter_id = self._get_or_create_chapter(code_str)
                block_id = self._get_or_create_block(code_str, chapter_id)
                category_id = self._get_or_create_category(code_str, block_id, desc)

                yield Code(
                    id=generate_uuid(),
                    category_id=category_id,
                    code=code_str,
                    short_description=desc[:100],
                    long_description=desc,
                    billable=len(code_str) > 4,  # Codes with decimals are typically billable
                    edition=2025,
                )

        except Exception as e:
            logger.error(f"GitHub download failed: {e}")
            raise

    def _parse_cms_txt(self, content: str) -> Generator[Code, None, None]:
        """Parse CMS tabular order text file format."""
        for line in content.split("\n"):
            line = line.strip()
            if not line:
                continue

            # CMS format: "code description" or "code  description" (variable spacing)
            # First 7-8 chars are code, rest is description
            match = re.match(r"^([A-Z]\d{2}\.?\d{0,4})\s+(.+)$", line, re.IGNORECASE)
            if not match:
                # Try fixed-width format
                if len(line) > 8:
                    code_str = line[:8].strip()
                    desc = line[8:].strip()
                    if code_str and desc and code_str[0].isalpha():
                        match = True
                    else:
                        continue
                else:
                    continue

            if match and not isinstance(match, bool):
                code_str = match.group(1).upper()
                desc = match.group(2)
            else:
                pass  # Already set above

            # Ensure code has decimal in right place
            if len(code_str) > 3 and "." not in code_str:
                code_str = code_str[:3] + "." + code_str[3:]

            chapter_id = self._get_or_create_chapter(code_str)
            block_id = self._get_or_create_block(code_str, chapter_id)
            category_id = self._get_or_create_category(code_str, block_id, desc)

            yield Code(
                id=generate_uuid(),
                category_id=category_id,
                code=code_str,
                short_description=desc[:100] if desc else "",
                long_description=desc if desc else "",
                billable=len(code_str.replace(".", "")) > 3,
                edition=2025,
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
    """Imports ICD-10-CM data into SQLite."""

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
                EffectiveFrom TEXT DEFAULT '2024-10-01',
                EffectiveTo TEXT DEFAULT '',
                Edition INTEGER DEFAULT 2025,
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
                EffectiveFrom TEXT DEFAULT '2024-10-01',
                EffectiveTo TEXT DEFAULT '',
                Edition INTEGER DEFAULT 2025,
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
            CREATE INDEX IF NOT EXISTS idx_achi_code_code ON achi_code(Code);
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
@click.option("--db-path", type=click.Path(), default="icd10.db", help="SQLite database path")
@click.option("--skip-embeddings", is_flag=True, help="Skip embedding generation")
@click.option("--use-github", is_flag=True, help="Use GitHub mirror instead of CMS.gov")
def main(db_path: str, skip_embeddings: bool, use_github: bool):
    """
    Import ICD-10-CM codes from CMS.gov (FREE, NO LICENSE REQUIRED).

    Downloads the official ICD-10-CM diagnosis codes from the US government
    and imports them into SQLite with optional vector embeddings for RAG search.
    """
    logger.info("=" * 60)
    logger.info("ICD-10-CM Import (FREE DATA from CMS.gov)")
    logger.info("=" * 60)

    downloader = ICD10CMDownloader()

    # Download codes
    if use_github:
        codes = list(tqdm(downloader.download_from_github(), desc="Downloading from GitHub"))
    else:
        codes = list(tqdm(downloader.download_from_cms(), desc="Downloading from CMS.gov"))

    logger.info(f"Downloaded {len(codes)} ICD-10-CM codes")
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
                logger.warning("Install with: pip install sentence-transformers")

        logger.info("=" * 60)
        logger.info(f"SUCCESS! Database created: {db_path}")
        logger.info(f"  - {len(codes)} diagnosis codes")
        logger.info(f"  - {len(downloader.chapters)} chapters")
        logger.info("=" * 60)

    except Exception as e:
        logger.error(f"Import failed: {e}")
        sys.exit(1)
    finally:
        importer.close()


if __name__ == "__main__":
    main()

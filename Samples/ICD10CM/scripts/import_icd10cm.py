#!/usr/bin/env python3
"""
ICD-10-CM Import Script (US Clinical Modification)

Imports ICD-10-CM diagnosis codes from CDC (FREE, Public Domain).
~74,000 codes from US Government.

Source: https://ftp.cdc.gov/pub/Health_Statistics/NCHS/Publications/ICD10CM/

Usage:
    python import_icd10cm.py --db-path ./icd10cm.db
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
from typing import Generator

import click
import requests
from tqdm import tqdm

logging.basicConfig(level=logging.INFO, format="%(asctime)s - %(levelname)s - %(message)s")
logger = logging.getLogger(__name__)

CDC_ICD10CM_URL = "https://ftp.cdc.gov/pub/Health_Statistics/NCHS/Publications/ICD10CM/2025/ICD10-CM%20Code%20Descriptions%202025.zip"
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


def generate_uuid() -> str:
    return str(uuid.uuid4())


def get_timestamp() -> str:
    return datetime.utcnow().isoformat() + "Z"


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
    "U": ("XXII", "Codes for special purposes", "U00", "U85"),
}


class ICD10CMDownloader:
    def __init__(self):
        self.chapters: dict[str, Chapter] = {}
        self.blocks: dict[str, Block] = {}
        self.categories: dict[str, Category] = {}

    def download(self) -> Generator[Code, None, None]:
        logger.info("Downloading ICD-10-CM from CDC...")
        response = requests.get(CDC_ICD10CM_URL, timeout=120)
        if response.status_code != 200:
            raise Exception(f"CDC download failed: HTTP {response.status_code}")
        logger.info("Downloaded CDC ICD-10-CM 2025")

        lines = []
        with zipfile.ZipFile(BytesIO(response.content)) as zf:
            for name in zf.namelist():
                if name.endswith(".txt"):
                    logger.info(f"Extracting {name}...")
                    with zf.open(name) as f:
                        lines.extend(f.read().decode("utf-8", errors="ignore").strip().split("\n"))

        logger.info(f"Loaded {len(lines)} lines")

        for line in lines:
            line = line.strip()
            if not line:
                continue
            if len(line) > 7:
                code_str, desc = line[:7].strip(), line[7:].strip()
            else:
                parts = line.split(None, 1)
                if len(parts) < 2:
                    continue
                code_str, desc = parts[0].strip(), parts[1].strip() if len(parts) > 1 else ""

            if not code_str or not re.match(r"^[A-Z]\d", code_str):
                continue
            if len(code_str) > 3 and "." not in code_str:
                code_str = code_str[:3] + "." + code_str[3:]

            chapter_id = self._get_or_create_chapter(code_str)
            block_id = self._get_or_create_block(code_str, chapter_id)
            category_id = self._get_or_create_category(code_str, block_id, desc)

            yield Code(generate_uuid(), category_id, code_str.upper(), desc[:100], desc, len(code_str.replace(".", "")) > 3)

    def _get_or_create_chapter(self, code: str) -> str:
        prefix = code[0].upper() if code else "Z"
        info = CHAPTER_MAP.get(prefix, ("XXI", "Unknown", "Z00", "Z99"))
        if info[0] not in self.chapters:
            self.chapters[info[0]] = Chapter(generate_uuid(), info[0], info[1], info[2], info[3])
        return self.chapters[info[0]].id

    def _get_or_create_block(self, code: str, chapter_id: str) -> str:
        block_code = code[:3].replace(".", "") if len(code) >= 3 else code
        if block_code not in self.blocks:
            self.blocks[block_code] = Block(generate_uuid(), chapter_id, block_code, f"Block {block_code}", block_code, block_code)
        return self.blocks[block_code].id

    def _get_or_create_category(self, code: str, block_id: str, title: str = "") -> str:
        cat_code = code[:3].replace(".", "") if len(code) >= 3 else code
        if cat_code not in self.categories:
            self.categories[cat_code] = Category(generate_uuid(), block_id, cat_code, title[:100] if title else f"Category {cat_code}")
        return self.categories[cat_code].id


class SQLiteImporter:
    def __init__(self, db_path: str):
        self.conn = sqlite3.connect(db_path)
        self._create_schema()

    def _create_schema(self):
        self.conn.executescript("""
            CREATE TABLE IF NOT EXISTS icd10cm_chapter (Id TEXT PRIMARY KEY, ChapterNumber TEXT UNIQUE, Title TEXT, CodeRangeStart TEXT, CodeRangeEnd TEXT, LastUpdated TEXT, VersionId INTEGER DEFAULT 1);
            CREATE TABLE IF NOT EXISTS icd10cm_block (Id TEXT PRIMARY KEY, ChapterId TEXT, BlockCode TEXT UNIQUE, Title TEXT, CodeRangeStart TEXT, CodeRangeEnd TEXT, LastUpdated TEXT, VersionId INTEGER DEFAULT 1);
            CREATE TABLE IF NOT EXISTS icd10cm_category (Id TEXT PRIMARY KEY, BlockId TEXT, CategoryCode TEXT UNIQUE, Title TEXT, LastUpdated TEXT, VersionId INTEGER DEFAULT 1);
            CREATE TABLE IF NOT EXISTS icd10cm_code (Id TEXT PRIMARY KEY, CategoryId TEXT, Code TEXT UNIQUE, ShortDescription TEXT, LongDescription TEXT, Billable INTEGER DEFAULT 1, EffectiveFrom TEXT DEFAULT '2025-10-01', EffectiveTo TEXT DEFAULT '', LastUpdated TEXT, VersionId INTEGER DEFAULT 1);
            CREATE TABLE IF NOT EXISTS icd10cm_code_embedding (Id TEXT PRIMARY KEY, CodeId TEXT UNIQUE, Embedding TEXT, EmbeddingModel TEXT DEFAULT 'MedEmbed-Small-v0.1', LastUpdated TEXT);
            CREATE INDEX IF NOT EXISTS idx_icd10cm_code_code ON icd10cm_code(Code);
            CREATE INDEX IF NOT EXISTS idx_icd10cm_code_desc ON icd10cm_code(ShortDescription);
        """)
        self.conn.commit()

    def import_chapters(self, chapters: list[Chapter]):
        logger.info(f"Importing {len(chapters)} chapters")
        for c in chapters:
            self.conn.execute("INSERT OR REPLACE INTO icd10cm_chapter VALUES (?,?,?,?,?,?,1)", (c.id, c.chapter_number, c.title, c.code_range_start, c.code_range_end, get_timestamp()))
        self.conn.commit()

    def import_blocks(self, blocks: list[Block]):
        logger.info(f"Importing {len(blocks)} blocks")
        for b in blocks:
            self.conn.execute("INSERT OR REPLACE INTO icd10cm_block VALUES (?,?,?,?,?,?,?,1)", (b.id, b.chapter_id, b.block_code, b.title, b.code_range_start, b.code_range_end, get_timestamp()))
        self.conn.commit()

    def import_categories(self, categories: list[Category]):
        logger.info(f"Importing {len(categories)} categories")
        for c in categories:
            self.conn.execute("INSERT OR REPLACE INTO icd10cm_category VALUES (?,?,?,?,?,1)", (c.id, c.block_id, c.category_code, c.title, get_timestamp()))
        self.conn.commit()

    def import_codes(self, codes: list[Code]):
        logger.info(f"Importing {len(codes)} codes")
        for c in codes:
            self.conn.execute("INSERT OR REPLACE INTO icd10cm_code VALUES (?,?,?,?,?,?,?,?,?,1)", (c.id, c.category_id, c.code, c.short_description, c.long_description, 1 if c.billable else 0, "2025-10-01", "", get_timestamp()))
        self.conn.commit()

    def close(self):
        self.conn.close()


@click.command()
@click.option("--db-path", default="icd10cm.db")
@click.option("--skip-embeddings", is_flag=True)
def main(db_path: str, skip_embeddings: bool):
    logger.info("=" * 60)
    logger.info("ICD-10-CM Import (CDC - US Clinical Modification)")
    logger.info("=" * 60)

    downloader = ICD10CMDownloader()
    codes = list(tqdm(downloader.download(), desc="ICD-10-CM"))
    logger.info(f"Total: {len(codes)} codes, {len(downloader.chapters)} chapters, {len(downloader.blocks)} blocks")

    importer = SQLiteImporter(db_path)
    try:
        importer.import_chapters(list(downloader.chapters.values()))
        importer.import_blocks(list(downloader.blocks.values()))
        importer.import_categories(list(downloader.categories.values()))
        importer.import_codes(codes)
        logger.info(f"SUCCESS! {len(codes)} ICD-10-CM codes in {db_path}")
    finally:
        importer.close()


if __name__ == "__main__":
    main()

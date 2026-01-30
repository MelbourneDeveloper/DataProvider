#!/usr/bin/env python3
"""
ICD-10-AM/ACHI Import Script

Imports ICD-10-AM diagnosis codes and ACHI procedure codes from IHACPA data files
into PostgreSQL with vector embeddings for RAG search.

Data Source: https://www.ihacpa.gov.au/resources/icd-10-amachiacs-resources

Usage:
    python import_icd10am.py --source /path/to/data --db-url postgresql://... --edition 13
"""

import json
import logging
import os
import sys
import uuid
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Generator

import click
import pandas as pd
import psycopg2
from dotenv import load_dotenv
from psycopg2.extras import execute_batch
from sentence_transformers import SentenceTransformer
from tqdm import tqdm

load_dotenv()

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)

EMBEDDING_MODEL = "abhinand5/medembed-large-v0.1"
EMBEDDING_DIMENSIONS = 1024
BATCH_SIZE = 100


@dataclass(frozen=True)
class Chapter:
    """ICD-10-AM Chapter record."""

    id: str
    chapter_number: str
    title: str
    code_range_start: str
    code_range_end: str


@dataclass(frozen=True)
class Block:
    """ICD-10-AM Block record."""

    id: str
    chapter_id: str
    block_code: str
    title: str
    code_range_start: str
    code_range_end: str


@dataclass(frozen=True)
class Category:
    """ICD-10-AM Category record."""

    id: str
    block_id: str
    category_code: str
    title: str


@dataclass(frozen=True)
class Code:
    """ICD-10-AM Code record."""

    id: str
    category_id: str
    code: str
    short_description: str
    long_description: str
    inclusion_terms: str
    exclusion_terms: str
    code_also: str
    code_first: str
    billable: bool
    effective_from: str
    effective_to: str
    edition: int


@dataclass(frozen=True)
class AchiBlock:
    """ACHI Block record."""

    id: str
    block_number: str
    title: str
    code_range_start: str
    code_range_end: str


@dataclass(frozen=True)
class AchiCode:
    """ACHI Code record."""

    id: str
    block_id: str
    code: str
    short_description: str
    long_description: str
    billable: bool
    effective_from: str
    effective_to: str
    edition: int


def generate_uuid() -> str:
    """Generate a UUID string."""
    return str(uuid.uuid4())


def get_timestamp() -> str:
    """Get current timestamp in ISO format."""
    return datetime.utcnow().isoformat() + "Z"


class EmbeddingGenerator:
    """Generates medical embeddings using MedEmbed model."""

    def __init__(self, model_name: str = EMBEDDING_MODEL):
        logger.info(f"Loading embedding model: {model_name}")
        self.model = SentenceTransformer(model_name)
        logger.info("Embedding model loaded successfully")

    def generate(self, text: str) -> list[float]:
        """Generate embedding for a single text."""
        embedding = self.model.encode(text, normalize_embeddings=True)
        return embedding.tolist()

    def generate_batch(self, texts: list[str]) -> list[list[float]]:
        """Generate embeddings for a batch of texts."""
        embeddings = self.model.encode(
            texts, normalize_embeddings=True, show_progress_bar=False
        )
        return [e.tolist() for e in embeddings]


class ICD10AMParser:
    """Parses ICD-10-AM data files from IHACPA."""

    def __init__(self, source_path: Path, edition: int):
        self.source_path = source_path
        self.edition = edition
        self.chapters: dict[str, Chapter] = {}
        self.blocks: dict[str, Block] = {}
        self.categories: dict[str, Category] = {}

    def parse_tabular_list(self) -> Generator[Code, None, None]:
        """
        Parse the ICD-10-AM tabular list.

        Expected file formats:
        - Excel (.xlsx) with columns: Code, ShortDescription, LongDescription, etc.
        - CSV with similar structure

        Note: Actual IHACPA files may have different formats.
        Adjust parsing logic based on actual data structure.
        """
        tabular_files = list(self.source_path.glob("*tabular*.xlsx")) + list(
            self.source_path.glob("*tabular*.csv")
        )

        if not tabular_files:
            logger.warning("No tabular list files found. Using sample data structure.")
            yield from self._generate_sample_codes()
            return

        for file_path in tabular_files:
            logger.info(f"Parsing tabular list: {file_path}")
            yield from self._parse_tabular_file(file_path)

    def _parse_tabular_file(self, file_path: Path) -> Generator[Code, None, None]:
        """Parse a single tabular list file."""
        if file_path.suffix == ".xlsx":
            df = pd.read_excel(file_path)
        else:
            df = pd.read_csv(file_path)

        required_columns = ["Code", "Description"]
        if not all(col in df.columns for col in required_columns):
            logger.error(f"Missing required columns in {file_path}")
            return

        for _, row in df.iterrows():
            code_str = str(row["Code"]).strip()
            if not code_str or code_str == "nan":
                continue

            chapter_id = self._get_or_create_chapter(code_str)
            block_id = self._get_or_create_block(code_str, chapter_id)
            category_id = self._get_or_create_category(code_str, block_id)

            yield Code(
                id=generate_uuid(),
                category_id=category_id,
                code=code_str,
                short_description=str(row.get("Description", ""))[:100],
                long_description=str(row.get("Description", "")),
                inclusion_terms=str(row.get("Includes", "")),
                exclusion_terms=str(row.get("Excludes", "")),
                code_also=str(row.get("CodeAlso", "")),
                code_first=str(row.get("CodeFirst", "")),
                billable=row.get("Billable", True),
                effective_from=str(row.get("EffectiveFrom", "2025-07-01")),
                effective_to=str(row.get("EffectiveTo", "")),
                edition=self.edition,
            )

    def _get_or_create_chapter(self, code: str) -> str:
        """Get or create chapter based on code prefix."""
        chapter_map = {
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
            "M": (
                "XIII",
                "Diseases of the musculoskeletal system and connective tissue",
                "M00",
                "M99",
            ),
            "N": ("XIV", "Diseases of the genitourinary system", "N00", "N99"),
            "O": ("XV", "Pregnancy, childbirth and the puerperium", "O00", "O99"),
            "P": (
                "XVI",
                "Certain conditions originating in the perinatal period",
                "P00",
                "P96",
            ),
            "Q": (
                "XVII",
                "Congenital malformations, deformations and chromosomal abnormalities",
                "Q00",
                "Q99",
            ),
            "R": (
                "XVIII",
                "Symptoms, signs and abnormal clinical and laboratory findings",
                "R00",
                "R99",
            ),
            "S": (
                "XIX",
                "Injury, poisoning and certain other consequences of external causes",
                "S00",
                "T98",
            ),
            "T": (
                "XIX",
                "Injury, poisoning and certain other consequences of external causes",
                "S00",
                "T98",
            ),
            "V": ("XX", "External causes of morbidity and mortality", "V01", "Y98"),
            "W": ("XX", "External causes of morbidity and mortality", "V01", "Y98"),
            "X": ("XX", "External causes of morbidity and mortality", "V01", "Y98"),
            "Y": ("XX", "External causes of morbidity and mortality", "V01", "Y98"),
            "Z": (
                "XXI",
                "Factors influencing health status and contact with health services",
                "Z00",
                "Z99",
            ),
            "U": ("XXII", "Codes for special purposes", "U00", "U99"),
        }

        prefix = code[0].upper() if code else "Z"
        chapter_info = chapter_map.get(prefix, ("XXI", "Unknown", "Z00", "Z99"))
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
        """Get or create block based on code prefix."""
        block_code = code[:3] if len(code) >= 3 else code
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

    def _get_or_create_category(self, code: str, block_id: str) -> str:
        """Get or create category based on 3-character code."""
        category_code = code[:3] if len(code) >= 3 else code
        if category_code not in self.categories:
            self.categories[category_code] = Category(
                id=generate_uuid(),
                block_id=block_id,
                category_code=category_code,
                title=f"Category {category_code}",
            )
        return self.categories[category_code].id

    def _generate_sample_codes(self) -> Generator[Code, None, None]:
        """Generate sample ICD-10-AM codes for testing."""
        sample_codes = [
            ("A00.0", "Cholera due to Vibrio cholerae 01, biovar cholerae"),
            ("A00.1", "Cholera due to Vibrio cholerae 01, biovar eltor"),
            ("I10", "Essential (primary) hypertension"),
            ("I21.0", "Acute transmural myocardial infarction of anterior wall"),
            ("J18.9", "Pneumonia, unspecified"),
            ("R07.4", "Chest pain, unspecified"),
            ("R06.0", "Dyspnoea"),
            ("E11.9", "Type 2 diabetes mellitus without complications"),
            ("K21.0", "Gastro-oesophageal reflux disease with oesophagitis"),
            ("M54.5", "Low back pain"),
        ]

        for code, description in sample_codes:
            chapter_id = self._get_or_create_chapter(code)
            block_id = self._get_or_create_block(code, chapter_id)
            category_id = self._get_or_create_category(code, block_id)

            yield Code(
                id=generate_uuid(),
                category_id=category_id,
                code=code,
                short_description=description[:100],
                long_description=description,
                inclusion_terms="",
                exclusion_terms="",
                code_also="",
                code_first="",
                billable=True,
                effective_from="2025-07-01",
                effective_to="",
                edition=self.edition,
            )


class ACHIParser:
    """Parses ACHI procedure codes from IHACPA data."""

    def __init__(self, source_path: Path, edition: int):
        self.source_path = source_path
        self.edition = edition
        self.blocks: dict[str, AchiBlock] = {}

    def parse_procedures(self) -> Generator[AchiCode, None, None]:
        """Parse ACHI procedure codes."""
        achi_files = list(self.source_path.glob("*achi*.xlsx")) + list(
            self.source_path.glob("*achi*.csv")
        )

        if not achi_files:
            logger.warning("No ACHI files found. Using sample data.")
            yield from self._generate_sample_procedures()
            return

        for file_path in achi_files:
            logger.info(f"Parsing ACHI file: {file_path}")
            yield from self._parse_achi_file(file_path)

    def _parse_achi_file(self, file_path: Path) -> Generator[AchiCode, None, None]:
        """Parse a single ACHI file."""
        if file_path.suffix == ".xlsx":
            df = pd.read_excel(file_path)
        else:
            df = pd.read_csv(file_path)

        for _, row in df.iterrows():
            code_str = str(row.get("Code", "")).strip()
            if not code_str or code_str == "nan":
                continue

            block_id = self._get_or_create_block(code_str)

            yield AchiCode(
                id=generate_uuid(),
                block_id=block_id,
                code=code_str,
                short_description=str(row.get("Description", ""))[:100],
                long_description=str(row.get("Description", "")),
                billable=True,
                effective_from="2025-07-01",
                effective_to="",
                edition=self.edition,
            )

    def _get_or_create_block(self, code: str) -> str:
        """Get or create ACHI block."""
        block_num = code[:4] if len(code) >= 4 else code
        if block_num not in self.blocks:
            self.blocks[block_num] = AchiBlock(
                id=generate_uuid(),
                block_number=block_num,
                title=f"Block {block_num}",
                code_range_start=block_num,
                code_range_end=block_num,
            )
        return self.blocks[block_num].id

    def _generate_sample_procedures(self) -> Generator[AchiCode, None, None]:
        """Generate sample ACHI codes for testing."""
        sample_procedures = [
            ("38497-00", "Coronary angiography"),
            ("38500-00", "Percutaneous transluminal coronary angioplasty"),
            ("90661-00", "Appendicectomy"),
            ("30571-00", "Cholecystectomy"),
            ("48900-00", "Total hip replacement"),
        ]

        for code, description in sample_procedures:
            block_id = self._get_or_create_block(code)
            yield AchiCode(
                id=generate_uuid(),
                block_id=block_id,
                code=code,
                short_description=description,
                long_description=description,
                billable=True,
                effective_from="2025-07-01",
                effective_to="",
                edition=self.edition,
            )


class DatabaseImporter:
    """Imports ICD-10-AM and ACHI data into PostgreSQL."""

    def __init__(self, db_url: str):
        self.conn = psycopg2.connect(db_url)
        self.conn.autocommit = False

    def close(self):
        """Close database connection."""
        self.conn.close()

    def import_chapters(self, chapters: list[Chapter]):
        """Import ICD-10-AM chapters."""
        logger.info(f"Importing {len(chapters)} chapters")
        with self.conn.cursor() as cur:
            execute_batch(
                cur,
                """
                INSERT INTO icd10am_chapter
                (Id, ChapterNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
                VALUES (%s, %s, %s, %s, %s, %s, 1)
                ON CONFLICT (Id) DO UPDATE SET
                    Title = EXCLUDED.Title,
                    LastUpdated = EXCLUDED.LastUpdated,
                    VersionId = icd10am_chapter.VersionId + 1
                """,
                [
                    (
                        c.id,
                        c.chapter_number,
                        c.title,
                        c.code_range_start,
                        c.code_range_end,
                        get_timestamp(),
                    )
                    for c in chapters
                ],
            )
        self.conn.commit()

    def import_blocks(self, blocks: list[Block]):
        """Import ICD-10-AM blocks."""
        logger.info(f"Importing {len(blocks)} blocks")
        with self.conn.cursor() as cur:
            execute_batch(
                cur,
                """
                INSERT INTO icd10am_block
                (Id, ChapterId, BlockCode, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
                VALUES (%s, %s, %s, %s, %s, %s, %s, 1)
                ON CONFLICT (Id) DO UPDATE SET
                    Title = EXCLUDED.Title,
                    LastUpdated = EXCLUDED.LastUpdated,
                    VersionId = icd10am_block.VersionId + 1
                """,
                [
                    (
                        b.id,
                        b.chapter_id,
                        b.block_code,
                        b.title,
                        b.code_range_start,
                        b.code_range_end,
                        get_timestamp(),
                    )
                    for b in blocks
                ],
            )
        self.conn.commit()

    def import_categories(self, categories: list[Category]):
        """Import ICD-10-AM categories."""
        logger.info(f"Importing {len(categories)} categories")
        with self.conn.cursor() as cur:
            execute_batch(
                cur,
                """
                INSERT INTO icd10am_category
                (Id, BlockId, CategoryCode, Title, LastUpdated, VersionId)
                VALUES (%s, %s, %s, %s, %s, 1)
                ON CONFLICT (Id) DO UPDATE SET
                    Title = EXCLUDED.Title,
                    LastUpdated = EXCLUDED.LastUpdated,
                    VersionId = icd10am_category.VersionId + 1
                """,
                [
                    (c.id, c.block_id, c.category_code, c.title, get_timestamp())
                    for c in categories
                ],
            )
        self.conn.commit()

    def import_codes(self, codes: list[Code]):
        """Import ICD-10-AM codes."""
        logger.info(f"Importing {len(codes)} codes")
        with self.conn.cursor() as cur:
            execute_batch(
                cur,
                """
                INSERT INTO icd10am_code
                (Id, CategoryId, Code, ShortDescription, LongDescription,
                 InclusionTerms, ExclusionTerms, CodeAlso, CodeFirst,
                 Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated, VersionId)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, 1)
                ON CONFLICT (Id) DO UPDATE SET
                    ShortDescription = EXCLUDED.ShortDescription,
                    LongDescription = EXCLUDED.LongDescription,
                    LastUpdated = EXCLUDED.LastUpdated,
                    VersionId = icd10am_code.VersionId + 1
                """,
                [
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
                        1 if c.billable else 0,
                        c.effective_from,
                        c.effective_to,
                        c.edition,
                        get_timestamp(),
                    )
                    for c in codes
                ],
            )
        self.conn.commit()

    def import_embeddings(
        self,
        code_embeddings: list[tuple[str, str, list[float]]],
        table: str = "icd10am_code_embedding",
    ):
        """Import code embeddings."""
        logger.info(f"Importing {len(code_embeddings)} embeddings to {table}")
        with self.conn.cursor() as cur:
            execute_batch(
                cur,
                f"""
                INSERT INTO {table}
                (Id, CodeId, Embedding, EmbeddingModel, LastUpdated)
                VALUES (%s, %s, %s, %s, %s)
                ON CONFLICT (CodeId) DO UPDATE SET
                    Embedding = EXCLUDED.Embedding,
                    LastUpdated = EXCLUDED.LastUpdated
                """,
                [
                    (
                        generate_uuid(),
                        code_id,
                        json.dumps(embedding),
                        EMBEDDING_MODEL,
                        get_timestamp(),
                    )
                    for code_id, _, embedding in code_embeddings
                ],
            )
        self.conn.commit()

    def import_achi_blocks(self, blocks: list[AchiBlock]):
        """Import ACHI blocks."""
        logger.info(f"Importing {len(blocks)} ACHI blocks")
        with self.conn.cursor() as cur:
            execute_batch(
                cur,
                """
                INSERT INTO achi_block
                (Id, BlockNumber, Title, CodeRangeStart, CodeRangeEnd, LastUpdated, VersionId)
                VALUES (%s, %s, %s, %s, %s, %s, 1)
                ON CONFLICT (Id) DO UPDATE SET
                    Title = EXCLUDED.Title,
                    LastUpdated = EXCLUDED.LastUpdated,
                    VersionId = achi_block.VersionId + 1
                """,
                [
                    (
                        b.id,
                        b.block_number,
                        b.title,
                        b.code_range_start,
                        b.code_range_end,
                        get_timestamp(),
                    )
                    for b in blocks
                ],
            )
        self.conn.commit()

    def import_achi_codes(self, codes: list[AchiCode]):
        """Import ACHI codes."""
        logger.info(f"Importing {len(codes)} ACHI codes")
        with self.conn.cursor() as cur:
            execute_batch(
                cur,
                """
                INSERT INTO achi_code
                (Id, BlockId, Code, ShortDescription, LongDescription,
                 Billable, EffectiveFrom, EffectiveTo, Edition, LastUpdated, VersionId)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, 1)
                ON CONFLICT (Id) DO UPDATE SET
                    ShortDescription = EXCLUDED.ShortDescription,
                    LongDescription = EXCLUDED.LongDescription,
                    LastUpdated = EXCLUDED.LastUpdated,
                    VersionId = achi_code.VersionId + 1
                """,
                [
                    (
                        c.id,
                        c.block_id,
                        c.code,
                        c.short_description,
                        c.long_description,
                        1 if c.billable else 0,
                        c.effective_from,
                        c.effective_to,
                        c.edition,
                        get_timestamp(),
                    )
                    for c in codes
                ],
            )
        self.conn.commit()


@click.command()
@click.option(
    "--source",
    type=click.Path(exists=True, path_type=Path),
    required=True,
    help="Path to IHACPA data files directory",
)
@click.option(
    "--db-url",
    envvar="DATABASE_URL",
    required=True,
    help="PostgreSQL connection string",
)
@click.option(
    "--edition",
    type=int,
    default=13,
    help="ICD-10-AM edition number (default: 13)",
)
@click.option(
    "--skip-embeddings",
    is_flag=True,
    help="Skip embedding generation (faster, for testing)",
)
def main(source: Path, db_url: str, edition: int, skip_embeddings: bool):
    """
    Import ICD-10-AM and ACHI codes into PostgreSQL.

    Downloads data from IHACPA, parses classification codes,
    generates medical embeddings, and imports everything into
    PostgreSQL with pgvector support for RAG search.
    """
    logger.info(f"Starting ICD-10-AM import from {source}")
    logger.info(f"Edition: {edition}")

    embedding_generator = None if skip_embeddings else EmbeddingGenerator()

    icd_parser = ICD10AMParser(source, edition)
    achi_parser = ACHIParser(source, edition)

    codes: list[Code] = []
    for code in tqdm(icd_parser.parse_tabular_list(), desc="Parsing ICD-10-AM codes"):
        codes.append(code)

    achi_codes: list[AchiCode] = []
    for code in tqdm(achi_parser.parse_procedures(), desc="Parsing ACHI codes"):
        achi_codes.append(code)

    logger.info(f"Parsed {len(codes)} ICD-10-AM codes")
    logger.info(f"Parsed {len(achi_codes)} ACHI codes")
    logger.info(f"Created {len(icd_parser.chapters)} chapters")
    logger.info(f"Created {len(icd_parser.blocks)} blocks")
    logger.info(f"Created {len(icd_parser.categories)} categories")

    importer = DatabaseImporter(db_url)

    try:
        importer.import_chapters(list(icd_parser.chapters.values()))
        importer.import_blocks(list(icd_parser.blocks.values()))
        importer.import_categories(list(icd_parser.categories.values()))
        importer.import_codes(codes)

        importer.import_achi_blocks(list(achi_parser.blocks.values()))
        importer.import_achi_codes(achi_codes)

        if embedding_generator:
            logger.info("Generating ICD-10-AM embeddings...")
            icd_embeddings = []
            for i in tqdm(range(0, len(codes), BATCH_SIZE), desc="ICD-10-AM embeddings"):
                batch = codes[i : i + BATCH_SIZE]
                texts = [
                    f"{c.code} {c.short_description} {c.long_description}" for c in batch
                ]
                embeddings = embedding_generator.generate_batch(texts)
                for code, embedding in zip(batch, embeddings):
                    icd_embeddings.append((code.id, code.code, embedding))

            importer.import_embeddings(icd_embeddings, "icd10am_code_embedding")

            logger.info("Generating ACHI embeddings...")
            achi_embeddings = []
            for i in tqdm(
                range(0, len(achi_codes), BATCH_SIZE), desc="ACHI embeddings"
            ):
                batch = achi_codes[i : i + BATCH_SIZE]
                texts = [
                    f"{c.code} {c.short_description} {c.long_description}" for c in batch
                ]
                embeddings = embedding_generator.generate_batch(texts)
                for code, embedding in zip(batch, embeddings):
                    achi_embeddings.append((code.id, code.code, embedding))

            importer.import_embeddings(achi_embeddings, "achi_code_embedding")

        logger.info("Import completed successfully!")

    except Exception as e:
        logger.error(f"Import failed: {e}")
        sys.exit(1)
    finally:
        importer.close()


if __name__ == "__main__":
    main()

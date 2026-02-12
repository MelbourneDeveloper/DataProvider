using Npgsql;

namespace ICD10.Api.Tests;

/// <summary>
/// Seeds ICD-10 reference data into a PostgreSQL test database.
/// All column names are lowercase to match PostgresDdlGenerator output.
/// </summary>
internal static class TestDataSeeder
{
    internal static void Seed(NpgsqlConnection conn)
    {
        SeedChapters(conn);
        SeedBlocks(conn);
        SeedCategories(conn);
        SeedCodes(conn);
        SeedAchiBlocks(conn);
        SeedAchiCodes(conn);
    }

    private static void SeedChapters(NpgsqlConnection conn)
    {
        // All 21 ICD-10-CM chapters with numeric chapter numbers
        var chapters = new (string Id, string Number, string Title, string Start, string End)[]
        {
            ("ch-01", "1", "Certain infectious and parasitic diseases", "A00", "B99"),
            ("ch-02", "2", "Neoplasms", "C00", "D49"),
            ("ch-03", "3", "Diseases of the blood and blood-forming organs", "D50", "D89"),
            ("ch-04", "4", "Endocrine, nutritional and metabolic diseases", "E00", "E89"),
            ("ch-05", "5", "Mental, behavioral and neurodevelopmental disorders", "F01", "F99"),
            ("ch-06", "6", "Diseases of the nervous system", "G00", "G99"),
            ("ch-07", "7", "Diseases of the eye and adnexa", "H00", "H59"),
            ("ch-08", "8", "Diseases of the ear and mastoid process", "H60", "H95"),
            ("ch-09", "9", "Diseases of the circulatory system", "I00", "I99"),
            ("ch-10", "10", "Diseases of the respiratory system", "J00", "J99"),
            ("ch-11", "11", "Diseases of the digestive system", "K00", "K95"),
            ("ch-12", "12", "Diseases of the skin and subcutaneous tissue", "L00", "L99"),
            (
                "ch-13",
                "13",
                "Diseases of the musculoskeletal system and connective tissue",
                "M00",
                "M99"
            ),
            ("ch-14", "14", "Diseases of the genitourinary system", "N00", "N99"),
            ("ch-15", "15", "Pregnancy, childbirth and the puerperium", "O00", "O9A"),
            ("ch-16", "16", "Certain conditions originating in the perinatal period", "P00", "P96"),
            ("ch-17", "17", "Congenital malformations and chromosomal abnormalities", "Q00", "Q99"),
            (
                "ch-18",
                "18",
                "Symptoms, signs and abnormal clinical and laboratory findings",
                "R00",
                "R99"
            ),
            (
                "ch-19",
                "19",
                "Injury, poisoning and certain other consequences of external causes",
                "S00",
                "T88"
            ),
            ("ch-20", "20", "External causes of morbidity", "V00", "Y99"),
            (
                "ch-21",
                "21",
                "Factors influencing health status and contact with health services",
                "Z00",
                "Z99"
            ),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO "public"."icd10_chapter" ("id", "chapternumber", "title", "coderangestart", "coderangeend")
            VALUES (@id, @num, @title, @start, @end)
            """;

        var pId = cmd.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Text));
        var pNum = cmd.Parameters.Add(new NpgsqlParameter("@num", NpgsqlTypes.NpgsqlDbType.Text));
        var pTitle = cmd.Parameters.Add(
            new NpgsqlParameter("@title", NpgsqlTypes.NpgsqlDbType.Text)
        );
        var pStart = cmd.Parameters.Add(
            new NpgsqlParameter("@start", NpgsqlTypes.NpgsqlDbType.Text)
        );
        var pEnd = cmd.Parameters.Add(new NpgsqlParameter("@end", NpgsqlTypes.NpgsqlDbType.Text));

        cmd.Prepare();

        foreach (var (id, number, title, start, end) in chapters)
        {
            pId.Value = id;
            pNum.Value = number;
            pTitle.Value = title;
            pStart.Value = start;
            pEnd.Value = end;
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedBlocks(NpgsqlConnection conn)
    {
        var blocks = new (
            string Id,
            string ChapterId,
            string BlockCode,
            string Title,
            string Start,
            string End
        )[]
        {
            ("blk-a00-a09", "ch-01", "A00-A09", "Intestinal infectious diseases", "A00", "A09"),
            ("blk-e10-e14", "ch-04", "E10-E14", "Diabetes mellitus", "E10", "E14"),
            ("blk-i10-i15", "ch-09", "I10-I15", "Hypertensive diseases", "I10", "I15"),
            ("blk-i20-i25", "ch-09", "I20-I25", "Ischaemic heart diseases", "I20", "I25"),
            ("blk-j00-j06", "ch-10", "J00-J06", "Acute upper respiratory infections", "J00", "J06"),
            (
                "blk-r00-r09",
                "ch-18",
                "R00-R09",
                "Symptoms and signs involving the circulatory and respiratory systems",
                "R00",
                "R09"
            ),
            ("blk-r50-r69", "ch-18", "R50-R69", "General symptoms and signs", "R50", "R69"),
            ("blk-m50-m54", "ch-13", "M50-M54", "Other dorsopathies", "M50", "M54"),
            ("blk-s70-s79", "ch-19", "S70-S79", "Injuries to the hip and thigh", "S70", "S79"),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO "public"."icd10_block" ("id", "chapterid", "blockcode", "title", "coderangestart", "coderangeend")
            VALUES (@id, @chid, @code, @title, @start, @end)
            """;

        var pId = cmd.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Text));
        var pChId = cmd.Parameters.Add(new NpgsqlParameter("@chid", NpgsqlTypes.NpgsqlDbType.Text));
        var pCode = cmd.Parameters.Add(new NpgsqlParameter("@code", NpgsqlTypes.NpgsqlDbType.Text));
        var pTitle = cmd.Parameters.Add(
            new NpgsqlParameter("@title", NpgsqlTypes.NpgsqlDbType.Text)
        );
        var pStart = cmd.Parameters.Add(
            new NpgsqlParameter("@start", NpgsqlTypes.NpgsqlDbType.Text)
        );
        var pEnd = cmd.Parameters.Add(new NpgsqlParameter("@end", NpgsqlTypes.NpgsqlDbType.Text));

        cmd.Prepare();

        foreach (var (id, chapterId, blockCode, title, start, end) in blocks)
        {
            pId.Value = id;
            pChId.Value = chapterId;
            pCode.Value = blockCode;
            pTitle.Value = title;
            pStart.Value = start;
            pEnd.Value = end;
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedCategories(NpgsqlConnection conn)
    {
        var categories = new (string Id, string BlockId, string CategoryCode, string Title)[]
        {
            ("cat-a00", "blk-a00-a09", "A00", "Cholera"),
            ("cat-e10", "blk-e10-e14", "E10", "Type 1 diabetes mellitus"),
            ("cat-e11", "blk-e10-e14", "E11", "Type 2 diabetes mellitus"),
            ("cat-i10", "blk-i10-i15", "I10", "Essential (primary) hypertension"),
            ("cat-i21", "blk-i20-i25", "I21", "Acute myocardial infarction"),
            (
                "cat-j06",
                "blk-j00-j06",
                "J06",
                "Acute upper respiratory infections of multiple and unspecified sites"
            ),
            ("cat-r06", "blk-r00-r09", "R06", "Abnormalities of breathing"),
            ("cat-r07", "blk-r00-r09", "R07", "Pain in throat and chest"),
            ("cat-r10", "blk-r00-r09", "R10", "Abdominal and pelvic pain"),
            ("cat-m54", "blk-m50-m54", "M54", "Dorsalgia"),
            ("cat-s72", "blk-s70-s79", "S72", "Fracture of femur"),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO "public"."icd10_category" ("id", "blockid", "categorycode", "title")
            VALUES (@id, @bid, @code, @title)
            """;

        var pId = cmd.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Text));
        var pBid = cmd.Parameters.Add(new NpgsqlParameter("@bid", NpgsqlTypes.NpgsqlDbType.Text));
        var pCode = cmd.Parameters.Add(new NpgsqlParameter("@code", NpgsqlTypes.NpgsqlDbType.Text));
        var pTitle = cmd.Parameters.Add(
            new NpgsqlParameter("@title", NpgsqlTypes.NpgsqlDbType.Text)
        );

        cmd.Prepare();

        foreach (var (id, blockId, categoryCode, title) in categories)
        {
            pId.Value = id;
            pBid.Value = blockId;
            pCode.Value = categoryCode;
            pTitle.Value = title;
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedCodes(NpgsqlConnection conn)
    {
        // All codes required by tests
        var codes = new (string Id, string CategoryId, string Code, string Short, string Long)[]
        {
            (
                "code-a00-0",
                "cat-a00",
                "A00.0",
                "Cholera due to Vibrio cholerae 01, biovar cholerae",
                "Cholera due to Vibrio cholerae 01, biovar cholerae"
            ),
            (
                "code-e10-9",
                "cat-e10",
                "E10.9",
                "Type 1 diabetes mellitus without complications",
                "Type 1 diabetes mellitus without complications"
            ),
            (
                "code-e11-9",
                "cat-e11",
                "E11.9",
                "Type 2 diabetes mellitus without complications",
                "Type 2 diabetes mellitus without complications"
            ),
            (
                "code-i10",
                "cat-i10",
                "I10",
                "Essential (primary) hypertension",
                "Essential (primary) hypertension"
            ),
            (
                "code-i21-0",
                "cat-i21",
                "I21.0",
                "Acute transmural myocardial infarction of anterior wall",
                "ST elevation (STEMI) myocardial infarction involving left main coronary artery"
            ),
            (
                "code-i21-11",
                "cat-i21",
                "I21.11",
                "ST elevation (STEMI) myocardial infarction involving right coronary artery",
                "ST elevation (STEMI) myocardial infarction involving right coronary artery"
            ),
            (
                "code-i21-4",
                "cat-i21",
                "I21.4",
                "Acute subendocardial myocardial infarction",
                "Non-ST elevation (NSTEMI) myocardial infarction"
            ),
            (
                "code-j06-9",
                "cat-j06",
                "J06.9",
                "Acute upper respiratory infection, unspecified",
                "Acute upper respiratory infection, unspecified"
            ),
            ("code-r06-00", "cat-r06", "R06.00", "Dyspnea, unspecified", "Dyspnea, unspecified"),
            (
                "code-r07-9",
                "cat-r07",
                "R07.9",
                "Chest pain, unspecified",
                "Chest pain, unspecified"
            ),
            ("code-r07-89", "cat-r07", "R07.89", "Other chest pain", "Other chest pain"),
            // Additional codes for search tests
            (
                "code-a00-1",
                "cat-a00",
                "A00.1",
                "Cholera due to Vibrio cholerae 01, biovar eltor",
                "Cholera due to Vibrio cholerae 01, biovar eltor"
            ),
            ("code-m54-5", "cat-m54", "M54.5", "Low back pain", "Low back pain"),
            (
                "code-s72-00",
                "cat-s72",
                "S72.00",
                "Fracture of neck of femur, closed",
                "Fracture of unspecified part of neck of femur, closed"
            ),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO "public"."icd10_code"
                ("id", "categoryid", "code", "shortdescription", "longdescription",
                 "inclusionterms", "exclusionterms", "codealso", "codefirst", "synonyms",
                 "billable", "effectivefrom", "effectiveto", "edition")
            VALUES (@id, @catid, @code, @short, @long,
                    '', '', '', '', '',
                    1, '2025-07-01', '', '2025')
            """;

        var pId = cmd.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Text));
        var pCatId = cmd.Parameters.Add(
            new NpgsqlParameter("@catid", NpgsqlTypes.NpgsqlDbType.Text)
        );
        var pCode = cmd.Parameters.Add(new NpgsqlParameter("@code", NpgsqlTypes.NpgsqlDbType.Text));
        var pShort = cmd.Parameters.Add(
            new NpgsqlParameter("@short", NpgsqlTypes.NpgsqlDbType.Text)
        );
        var pLong = cmd.Parameters.Add(new NpgsqlParameter("@long", NpgsqlTypes.NpgsqlDbType.Text));

        cmd.Prepare();

        foreach (var (id, categoryId, code, shortDesc, longDesc) in codes)
        {
            pId.Value = id;
            pCatId.Value = categoryId;
            pCode.Value = code;
            pShort.Value = shortDesc;
            pLong.Value = longDesc;
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedAchiBlocks(NpgsqlConnection conn)
    {
        var blocks = new (string Id, string BlockNumber, string Title, string Start, string End)[]
        {
            ("achi-blk-1", "1820", "Procedures on heart", "38497-00", "38503-00"),
            ("achi-blk-2", "0926", "Procedures on appendix", "90661-00", "90661-00"),
            (
                "achi-blk-3",
                "0965",
                "Procedures on gallbladder and biliary tract",
                "30571-00",
                "30575-00"
            ),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO "public"."achi_block" ("id", "blocknumber", "title", "coderangestart", "coderangeend")
            VALUES (@id, @num, @title, @start, @end)
            """;

        var pId = cmd.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Text));
        var pNum = cmd.Parameters.Add(new NpgsqlParameter("@num", NpgsqlTypes.NpgsqlDbType.Text));
        var pTitle = cmd.Parameters.Add(
            new NpgsqlParameter("@title", NpgsqlTypes.NpgsqlDbType.Text)
        );
        var pStart = cmd.Parameters.Add(
            new NpgsqlParameter("@start", NpgsqlTypes.NpgsqlDbType.Text)
        );
        var pEnd = cmd.Parameters.Add(new NpgsqlParameter("@end", NpgsqlTypes.NpgsqlDbType.Text));

        cmd.Prepare();

        foreach (var (id, number, title, start, end) in blocks)
        {
            pId.Value = id;
            pNum.Value = number;
            pTitle.Value = title;
            pStart.Value = start;
            pEnd.Value = end;
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedAchiCodes(NpgsqlConnection conn)
    {
        var codes = new (string Id, string BlockId, string Code, string Short, string Long)[]
        {
            (
                "achi-38497-00",
                "achi-blk-1",
                "38497-00",
                "Coronary angiography",
                "Coronary angiography"
            ),
            (
                "achi-38503-00",
                "achi-blk-1",
                "38503-00",
                "Percutaneous insertion of coronary artery stent",
                "Percutaneous insertion of coronary artery stent"
            ),
            ("achi-90661-00", "achi-blk-2", "90661-00", "Appendicectomy", "Appendicectomy"),
            ("achi-30571-00", "achi-blk-3", "30571-00", "Cholecystectomy", "Cholecystectomy"),
        };

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO "public"."achi_code"
                ("id", "blockid", "code", "shortdescription", "longdescription",
                 "billable", "effectivefrom", "effectiveto", "edition")
            VALUES (@id, @bid, @code, @short, @long,
                    1, '2025-07-01', '', '13')
            """;

        var pId = cmd.Parameters.Add(new NpgsqlParameter("@id", NpgsqlTypes.NpgsqlDbType.Text));
        var pBid = cmd.Parameters.Add(new NpgsqlParameter("@bid", NpgsqlTypes.NpgsqlDbType.Text));
        var pCode = cmd.Parameters.Add(new NpgsqlParameter("@code", NpgsqlTypes.NpgsqlDbType.Text));
        var pShort = cmd.Parameters.Add(
            new NpgsqlParameter("@short", NpgsqlTypes.NpgsqlDbType.Text)
        );
        var pLong = cmd.Parameters.Add(new NpgsqlParameter("@long", NpgsqlTypes.NpgsqlDbType.Text));

        cmd.Prepare();

        foreach (var (id, blockId, code, shortDesc, longDesc) in codes)
        {
            pId.Value = id;
            pBid.Value = blockId;
            pCode.Value = code;
            pShort.Value = shortDesc;
            pLong.Value = longDesc;
            cmd.ExecuteNonQuery();
        }
    }
}

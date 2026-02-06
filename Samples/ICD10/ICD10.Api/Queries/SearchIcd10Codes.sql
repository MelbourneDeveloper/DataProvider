SELECT c."Id", c."Code", c."ShortDescription", c."LongDescription", c."Billable",
       cat."CategoryCode", cat."Title" AS "CategoryTitle",
       b."BlockCode", b."Title" AS "BlockTitle",
       ch."ChapterNumber", ch."Title" AS "ChapterTitle",
       c."InclusionTerms", c."ExclusionTerms", c."CodeAlso", c."CodeFirst", c."Synonyms", c."Edition"
FROM icd10_code c
LEFT JOIN icd10_category cat ON c."CategoryId" = cat."Id"
LEFT JOIN icd10_block b ON cat."BlockId" = b."Id"
LEFT JOIN icd10_chapter ch ON b."ChapterId" = ch."Id"
WHERE c."Code" ILIKE @term OR c."ShortDescription" ILIKE @term OR c."LongDescription" ILIKE @term
ORDER BY c."Code"
LIMIT @limit

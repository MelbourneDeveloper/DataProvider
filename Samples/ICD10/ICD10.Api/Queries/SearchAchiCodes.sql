SELECT "Id", "BlockId", "Code", "ShortDescription", "LongDescription", "Billable"
FROM achi_code
WHERE "Code" ILIKE @term OR "ShortDescription" ILIKE @term OR "LongDescription" ILIKE @term
ORDER BY "Code"
LIMIT @limit

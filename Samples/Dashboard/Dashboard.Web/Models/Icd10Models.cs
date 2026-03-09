using H5;

namespace Dashboard.Models
{
    /// <summary>
    /// ICD-10 chapter model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Icd10Chapter
    {
        /// <summary>Chapter unique identifier.</summary>
        [Name("Id")]
        public extern string Id { get; set; }

        /// <summary>Chapter number (1-22).</summary>
        [Name("ChapterNumber")]
        public extern string ChapterNumber { get; set; }

        /// <summary>Chapter title.</summary>
        [Name("Title")]
        public extern string Title { get; set; }

        /// <summary>Code range start.</summary>
        [Name("CodeRangeStart")]
        public extern string CodeRangeStart { get; set; }

        /// <summary>Code range end.</summary>
        [Name("CodeRangeEnd")]
        public extern string CodeRangeEnd { get; set; }
    }

    /// <summary>
    /// ICD-10 block model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Icd10Block
    {
        /// <summary>Block unique identifier.</summary>
        [Name("Id")]
        public extern string Id { get; set; }

        /// <summary>Chapter identifier.</summary>
        [Name("ChapterId")]
        public extern string ChapterId { get; set; }

        /// <summary>Block code.</summary>
        [Name("BlockCode")]
        public extern string BlockCode { get; set; }

        /// <summary>Block title.</summary>
        [Name("Title")]
        public extern string Title { get; set; }

        /// <summary>Code range start.</summary>
        [Name("CodeRangeStart")]
        public extern string CodeRangeStart { get; set; }

        /// <summary>Code range end.</summary>
        [Name("CodeRangeEnd")]
        public extern string CodeRangeEnd { get; set; }
    }

    /// <summary>
    /// ICD-10 category model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Icd10Category
    {
        /// <summary>Category unique identifier.</summary>
        [Name("Id")]
        public extern string Id { get; set; }

        /// <summary>Block identifier.</summary>
        [Name("BlockId")]
        public extern string BlockId { get; set; }

        /// <summary>Category code.</summary>
        [Name("CategoryCode")]
        public extern string CategoryCode { get; set; }

        /// <summary>Category title.</summary>
        [Name("Title")]
        public extern string Title { get; set; }
    }

    /// <summary>
    /// ICD-10 code model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Icd10Code
    {
        /// <summary>Code unique identifier.</summary>
        [Name("Id")]
        public extern string Id { get; set; }

        /// <summary>Category identifier.</summary>
        [Name("CategoryId")]
        public extern string CategoryId { get; set; }

        /// <summary>ICD-10 code value.</summary>
        [Name("Code")]
        public extern string Code { get; set; }

        /// <summary>Short description.</summary>
        [Name("ShortDescription")]
        public extern string ShortDescription { get; set; }

        /// <summary>Long description.</summary>
        [Name("LongDescription")]
        public extern string LongDescription { get; set; }

        /// <summary>Inclusion terms.</summary>
        [Name("InclusionTerms")]
        public extern string InclusionTerms { get; set; }

        /// <summary>Exclusion terms.</summary>
        [Name("ExclusionTerms")]
        public extern string ExclusionTerms { get; set; }

        /// <summary>Code also reference.</summary>
        [Name("CodeAlso")]
        public extern string CodeAlso { get; set; }

        /// <summary>Code first reference.</summary>
        [Name("CodeFirst")]
        public extern string CodeFirst { get; set; }

        /// <summary>Synonyms for the code.</summary>
        [Name("Synonyms")]
        public extern string Synonyms { get; set; }

        /// <summary>Whether code is billable.</summary>
        [Name("Billable")]
        public extern bool Billable { get; set; }

        /// <summary>Edition of the code.</summary>
        [Name("Edition")]
        public extern string Edition { get; set; }

        /// <summary>Category code.</summary>
        [Name("CategoryCode")]
        public extern string CategoryCode { get; set; }

        /// <summary>Category title.</summary>
        [Name("CategoryTitle")]
        public extern string CategoryTitle { get; set; }

        /// <summary>Block code.</summary>
        [Name("BlockCode")]
        public extern string BlockCode { get; set; }

        /// <summary>Block title.</summary>
        [Name("BlockTitle")]
        public extern string BlockTitle { get; set; }

        /// <summary>Chapter number.</summary>
        [Name("ChapterNumber")]
        public extern string ChapterNumber { get; set; }

        /// <summary>Chapter title.</summary>
        [Name("ChapterTitle")]
        public extern string ChapterTitle { get; set; }
    }

    /// <summary>
    /// ACHI procedure code model.
    /// </summary>
    [External]
    [Name("Object")]
    public class AchiCode
    {
        /// <summary>Code unique identifier.</summary>
        [Name("Id")]
        public extern string Id { get; set; }

        /// <summary>Block identifier.</summary>
        [Name("BlockId")]
        public extern string BlockId { get; set; }

        /// <summary>ACHI code value.</summary>
        [Name("Code")]
        public extern string Code { get; set; }

        /// <summary>Short description.</summary>
        [Name("ShortDescription")]
        public extern string ShortDescription { get; set; }

        /// <summary>Long description.</summary>
        [Name("LongDescription")]
        public extern string LongDescription { get; set; }

        /// <summary>Whether code is billable.</summary>
        [Name("Billable")]
        public extern bool Billable { get; set; }

        /// <summary>Block number.</summary>
        [Name("BlockNumber")]
        public extern string BlockNumber { get; set; }

        /// <summary>Block title.</summary>
        [Name("BlockTitle")]
        public extern string BlockTitle { get; set; }
    }

    /// <summary>
    /// Semantic search result model.
    /// </summary>
    [External]
    [Name("Object")]
    public class SemanticSearchResult
    {
        /// <summary>ICD-10 code.</summary>
        [Name("Code")]
        public extern string Code { get; set; }

        /// <summary>Code description.</summary>
        [Name("Description")]
        public extern string Description { get; set; }

        /// <summary>Long description with clinical details.</summary>
        [Name("LongDescription")]
        public extern string LongDescription { get; set; }

        /// <summary>Confidence score (0-1).</summary>
        [Name("Confidence")]
        public extern double Confidence { get; set; }

        /// <summary>Code type (ICD10CM or ACHI).</summary>
        [Name("CodeType")]
        public extern string CodeType { get; set; }

        /// <summary>Chapter number (e.g., "19").</summary>
        [Name("Chapter")]
        public extern string Chapter { get; set; }

        /// <summary>Chapter title (e.g., "Injury, poisoning and external causes").</summary>
        [Name("ChapterTitle")]
        public extern string ChapterTitle { get; set; }

        /// <summary>Category code (first 3 characters, e.g., "S70").</summary>
        [Name("Category")]
        public extern string Category { get; set; }

        /// <summary>Inclusion terms for the code.</summary>
        [Name("InclusionTerms")]
        public extern string InclusionTerms { get; set; }

        /// <summary>Exclusion terms for the code.</summary>
        [Name("ExclusionTerms")]
        public extern string ExclusionTerms { get; set; }

        /// <summary>Code also references.</summary>
        [Name("CodeAlso")]
        public extern string CodeAlso { get; set; }

        /// <summary>Code first references.</summary>
        [Name("CodeFirst")]
        public extern string CodeFirst { get; set; }
    }

    /// <summary>
    /// Search request model.
    /// </summary>
    public class SemanticSearchRequest
    {
        /// <summary>Search query text.</summary>
        public string Query { get; set; }

        /// <summary>Maximum results to return.</summary>
        public int Limit { get; set; }

        /// <summary>Whether to include ACHI codes.</summary>
        public bool IncludeAchi { get; set; }
    }

    /// <summary>
    /// Semantic search API response model.
    /// </summary>
    [External]
    [Name("Object")]
    public class SemanticSearchResponse
    {
        /// <summary>Search results.</summary>
        [Name("Results")]
        public extern SemanticSearchResult[] Results { get; set; }

        /// <summary>Original query.</summary>
        [Name("Query")]
        public extern string Query { get; set; }

        /// <summary>Model used for embeddings.</summary>
        [Name("Model")]
        public extern string Model { get; set; }
    }
}

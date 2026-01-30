using H5;

namespace Dashboard.Models
{
    /// <summary>
    /// ICD-10-AM chapter model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Icd10Chapter
    {
        /// <summary>Chapter unique identifier.</summary>
        public extern string Id { get; set; }

        /// <summary>Chapter number (1-22).</summary>
        public extern int ChapterNumber { get; set; }

        /// <summary>Chapter title.</summary>
        public extern string Title { get; set; }

        /// <summary>Code range start.</summary>
        public extern string CodeRangeStart { get; set; }

        /// <summary>Code range end.</summary>
        public extern string CodeRangeEnd { get; set; }
    }

    /// <summary>
    /// ICD-10-AM block model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Icd10Block
    {
        /// <summary>Block unique identifier.</summary>
        public extern string Id { get; set; }

        /// <summary>Chapter identifier.</summary>
        public extern string ChapterId { get; set; }

        /// <summary>Block code.</summary>
        public extern string BlockCode { get; set; }

        /// <summary>Block title.</summary>
        public extern string Title { get; set; }

        /// <summary>Code range start.</summary>
        public extern string CodeRangeStart { get; set; }

        /// <summary>Code range end.</summary>
        public extern string CodeRangeEnd { get; set; }
    }

    /// <summary>
    /// ICD-10-AM category model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Icd10Category
    {
        /// <summary>Category unique identifier.</summary>
        public extern string Id { get; set; }

        /// <summary>Block identifier.</summary>
        public extern string BlockId { get; set; }

        /// <summary>Category code.</summary>
        public extern string CategoryCode { get; set; }

        /// <summary>Category title.</summary>
        public extern string Title { get; set; }
    }

    /// <summary>
    /// ICD-10-AM code model.
    /// </summary>
    [External]
    [Name("Object")]
    public class Icd10Code
    {
        /// <summary>Code unique identifier.</summary>
        public extern string Id { get; set; }

        /// <summary>Category identifier.</summary>
        public extern string CategoryId { get; set; }

        /// <summary>ICD-10 code value.</summary>
        public extern string Code { get; set; }

        /// <summary>Short description.</summary>
        public extern string ShortDescription { get; set; }

        /// <summary>Long description.</summary>
        public extern string LongDescription { get; set; }

        /// <summary>Inclusion terms.</summary>
        public extern string InclusionTerms { get; set; }

        /// <summary>Exclusion terms.</summary>
        public extern string ExclusionTerms { get; set; }

        /// <summary>Code also reference.</summary>
        public extern string CodeAlso { get; set; }

        /// <summary>Code first reference.</summary>
        public extern string CodeFirst { get; set; }

        /// <summary>Whether code is billable.</summary>
        public extern bool Billable { get; set; }

        /// <summary>Category code.</summary>
        public extern string CategoryCode { get; set; }

        /// <summary>Block code.</summary>
        public extern string BlockCode { get; set; }

        /// <summary>Chapter number.</summary>
        public extern int ChapterNumber { get; set; }

        /// <summary>Chapter title.</summary>
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
        public extern string Id { get; set; }

        /// <summary>Block identifier.</summary>
        public extern string BlockId { get; set; }

        /// <summary>ACHI code value.</summary>
        public extern string Code { get; set; }

        /// <summary>Short description.</summary>
        public extern string ShortDescription { get; set; }

        /// <summary>Long description.</summary>
        public extern string LongDescription { get; set; }

        /// <summary>Whether code is billable.</summary>
        public extern bool Billable { get; set; }

        /// <summary>Block number.</summary>
        public extern int BlockNumber { get; set; }

        /// <summary>Block title.</summary>
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
        public extern string Code { get; set; }

        /// <summary>Code description.</summary>
        public extern string Description { get; set; }

        /// <summary>Confidence score (0-1).</summary>
        public extern double Confidence { get; set; }

        /// <summary>Code type (ICD10AM or ACHI).</summary>
        public extern string CodeType { get; set; }
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
}

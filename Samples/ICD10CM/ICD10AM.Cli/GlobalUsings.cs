global using OkChapters = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<Chapter>,
    Outcome.HttpError<ErrorResponse>
>.Ok<System.Collections.Immutable.ImmutableArray<Chapter>, Outcome.HttpError<ErrorResponse>>;
global using OkCode = Outcome.Result<Icd10Code, Outcome.HttpError<ErrorResponse>>.Ok<
    Icd10Code,
    Outcome.HttpError<ErrorResponse>
>;
global using OkCodes = Outcome.Result<
    System.Collections.Immutable.ImmutableArray<Icd10Code>,
    Outcome.HttpError<ErrorResponse>
>.Ok<System.Collections.Immutable.ImmutableArray<Icd10Code>, Outcome.HttpError<ErrorResponse>>;
global using OkHealth = Outcome.Result<HealthResponse, Outcome.HttpError<ErrorResponse>>.Ok<
    HealthResponse,
    Outcome.HttpError<ErrorResponse>
>;
global using OkSearch = Outcome.Result<SearchResponse, Outcome.HttpError<ErrorResponse>>.Ok<
    SearchResponse,
    Outcome.HttpError<ErrorResponse>
>;

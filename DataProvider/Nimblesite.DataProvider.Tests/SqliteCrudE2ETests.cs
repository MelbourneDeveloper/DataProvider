using Microsoft.Data.Sqlite;
using Outcome;

namespace Nimblesite.DataProvider.Tests;

/// <summary>
/// E2E tests: full CRUD workflows against real SQLite databases.
/// Each test is a complete user workflow with multiple operations and assertions.
/// </summary>
public sealed class SqliteCrudE2ETests : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"crud_e2e_{Guid.NewGuid()}.db"
    );

    private readonly SqliteConnection _connection;

    public SqliteCrudE2ETests()
    {
        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();
        CreateSchema();
    }

    public void Dispose()
    {
        _connection.Dispose();
        try
        {
            File.Delete(_dbPath);
        }
        catch (IOException)
        { /* cleanup best-effort */
        }
    }

    private void CreateSchema()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE Patients (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Age INTEGER NOT NULL,
                Email TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1
            );
            CREATE TABLE Appointments (
                Id TEXT PRIMARY KEY,
                PatientId TEXT NOT NULL,
                AppointmentDate TEXT NOT NULL,
                Notes TEXT,
                FOREIGN KEY (PatientId) REFERENCES Patients(Id)
            );
            CREATE TABLE Medications (
                Id TEXT PRIMARY KEY,
                PatientId TEXT NOT NULL,
                DrugName TEXT NOT NULL,
                Dosage TEXT NOT NULL,
                FOREIGN KEY (PatientId) REFERENCES Patients(Id)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void FullPatientLifecycle_InsertQueryUpdateDelete_AllOperationsSucceed()
    {
        // Insert multiple patients
        var patient1Id = Guid.NewGuid().ToString();
        var patient2Id = Guid.NewGuid().ToString();
        var patient3Id = Guid.NewGuid().ToString();

        var insert1 = _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age, Email, IsActive) VALUES (@id, @name, @age, @email, 1)",
            parameters:
            [
                new SqliteParameter("@id", patient1Id),
                new SqliteParameter("@name", "Alice Smith"),
                new SqliteParameter("@age", 30),
                new SqliteParameter("@email", "alice@example.com"),
            ]
        );
        var insert2 = _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age, Email, IsActive) VALUES (@id, @name, @age, @email, 1)",
            parameters:
            [
                new SqliteParameter("@id", patient2Id),
                new SqliteParameter("@name", "Bob Jones"),
                new SqliteParameter("@age", 45),
                new SqliteParameter("@email", "bob@example.com"),
            ]
        );
        var insert3 = _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age, Email, IsActive) VALUES (@id, @name, @age, @email, 0)",
            parameters:
            [
                new SqliteParameter("@id", patient3Id),
                new SqliteParameter("@name", "Charlie Brown"),
                new SqliteParameter("@age", 60),
                new SqliteParameter("@email", DBNull.Value),
            ]
        );

        // Verify inserts succeeded
        Assert.IsType<IntOk>(insert1);
        Assert.IsType<IntOk>(insert2);
        Assert.IsType<IntOk>(insert3);
        Assert.Equal(1, ((IntOk)insert1).Value);
        Assert.Equal(1, ((IntOk)insert2).Value);
        Assert.Equal(1, ((IntOk)insert3).Value);

        // Query all patients
        var allPatients = _connection.Query<(string Id, string Name, int Age)>(
            sql: "SELECT Id, Name, Age FROM Patients ORDER BY Name",
            mapper: r => (Id: r.GetString(0), Name: r.GetString(1), Age: r.GetInt32(2))
        );
        Assert.IsType<Result<IReadOnlyList<(string Id, string Name, int Age)>, SqlError>.Ok<
            IReadOnlyList<(string Id, string Name, int Age)>,
            SqlError
        >>(allPatients);
        var patients = (
            (Result<IReadOnlyList<(string Id, string Name, int Age)>, SqlError>.Ok<
                IReadOnlyList<(string Id, string Name, int Age)>,
                SqlError
            >)allPatients
        ).Value;
        Assert.Equal(3, patients.Count);
        Assert.Equal("Alice Smith", patients[0].Name);
        Assert.Equal("Bob Jones", patients[1].Name);
        Assert.Equal("Charlie Brown", patients[2].Name);

        // Query with WHERE filter
        var activePatients = _connection.Query<string>(
            sql: "SELECT Name FROM Patients WHERE IsActive = 1 ORDER BY Name",
            mapper: r => r.GetString(0)
        );
        Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError>>(
            activePatients
        );
        var activeList = (
            (Result<IReadOnlyList<string>, SqlError>.Ok<
                IReadOnlyList<string>,
                SqlError
            >)activePatients
        ).Value;
        Assert.Equal(2, activeList.Count);
        Assert.Contains("Alice Smith", activeList);
        Assert.Contains("Bob Jones", activeList);

        // Scalar: count patients
        var countResult = _connection.Scalar<long>(
            sql: "SELECT COUNT(*) FROM Patients WHERE Age > @minAge",
            parameters: [new SqliteParameter("@minAge", 35)]
        );
        Assert.IsType<Result<long?, SqlError>.Ok<long?, SqlError>>(countResult);
        Assert.Equal(2L, ((Result<long?, SqlError>.Ok<long?, SqlError>)countResult).Value);

        // Update a patient
        var updateResult = _connection.Execute(
            sql: "UPDATE Patients SET Age = @age, Email = @email WHERE Id = @id",
            parameters:
            [
                new SqliteParameter("@age", 31),
                new SqliteParameter("@email", "alice.smith@example.com"),
                new SqliteParameter("@id", patient1Id),
            ]
        );
        Assert.IsType<IntOk>(updateResult);
        Assert.Equal(1, ((IntOk)updateResult).Value);

        // Verify update
        var updatedAge = _connection.Scalar<long>(
            sql: "SELECT Age FROM Patients WHERE Id = @id",
            parameters: [new SqliteParameter("@id", patient1Id)]
        );
        Assert.Equal(31L, ((Result<long?, SqlError>.Ok<long?, SqlError>)updatedAge).Value);

        // Delete a patient
        var deleteResult = _connection.Execute(
            sql: "DELETE FROM Patients WHERE Id = @id",
            parameters: [new SqliteParameter("@id", patient3Id)]
        );
        Assert.IsType<IntOk>(deleteResult);
        Assert.Equal(1, ((IntOk)deleteResult).Value);

        // Verify delete
        var finalCount = _connection.Scalar<long>(sql: "SELECT COUNT(*) FROM Patients");
        Assert.Equal(2L, ((Result<long?, SqlError>.Ok<long?, SqlError>)finalCount).Value);
    }

    [Fact]
    public void MultiTableWorkflow_PatientsAppointmentsMedications_RelatedDataManagement()
    {
        // Insert patient
        var patientId = Guid.NewGuid().ToString();
        _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age, Email) VALUES (@id, @name, @age, @email)",
            parameters:
            [
                new SqliteParameter("@id", patientId),
                new SqliteParameter("@name", "Diana Prince"),
                new SqliteParameter("@age", 35),
                new SqliteParameter("@email", "diana@example.com"),
            ]
        );

        // Insert multiple appointments
        var apt1Id = Guid.NewGuid().ToString();
        var apt2Id = Guid.NewGuid().ToString();
        var apt3Id = Guid.NewGuid().ToString();
        _connection.Execute(
            sql: "INSERT INTO Appointments (Id, PatientId, AppointmentDate, Notes) VALUES (@id, @pid, @date, @notes)",
            parameters:
            [
                new SqliteParameter("@id", apt1Id),
                new SqliteParameter("@pid", patientId),
                new SqliteParameter("@date", "2026-01-15"),
                new SqliteParameter("@notes", "Annual checkup"),
            ]
        );
        _connection.Execute(
            sql: "INSERT INTO Appointments (Id, PatientId, AppointmentDate, Notes) VALUES (@id, @pid, @date, @notes)",
            parameters:
            [
                new SqliteParameter("@id", apt2Id),
                new SqliteParameter("@pid", patientId),
                new SqliteParameter("@date", "2026-02-20"),
                new SqliteParameter("@notes", "Follow-up"),
            ]
        );
        _connection.Execute(
            sql: "INSERT INTO Appointments (Id, PatientId, AppointmentDate, Notes) VALUES (@id, @pid, @date, @notes)",
            parameters:
            [
                new SqliteParameter("@id", apt3Id),
                new SqliteParameter("@pid", patientId),
                new SqliteParameter("@date", "2026-03-10"),
                new SqliteParameter("@notes", "Lab results review"),
            ]
        );

        // Insert medications
        var med1Id = Guid.NewGuid().ToString();
        var med2Id = Guid.NewGuid().ToString();
        _connection.Execute(
            sql: "INSERT INTO Medications (Id, PatientId, DrugName, Dosage) VALUES (@id, @pid, @drug, @dosage)",
            parameters:
            [
                new SqliteParameter("@id", med1Id),
                new SqliteParameter("@pid", patientId),
                new SqliteParameter("@drug", "Aspirin"),
                new SqliteParameter("@dosage", "100mg daily"),
            ]
        );
        _connection.Execute(
            sql: "INSERT INTO Medications (Id, PatientId, DrugName, Dosage) VALUES (@id, @pid, @drug, @dosage)",
            parameters:
            [
                new SqliteParameter("@id", med2Id),
                new SqliteParameter("@pid", patientId),
                new SqliteParameter("@drug", "Metformin"),
                new SqliteParameter("@dosage", "500mg twice daily"),
            ]
        );

        // Query with JOIN: patient + appointments
        var joinResult = _connection.Query<(string Name, string Date, string Notes)>(
            sql: """
            SELECT p.Name, a.AppointmentDate, a.Notes
            FROM Patients p
            INNER JOIN Appointments a ON p.Id = a.PatientId
            ORDER BY a.AppointmentDate
            """,
            mapper: r => (r.GetString(0), r.GetString(1), r.GetString(2))
        );
        Assert.IsType<Result<IReadOnlyList<(string, string, string)>, SqlError>.Ok<
            IReadOnlyList<(string, string, string)>,
            SqlError
        >>(joinResult);
        var appointments = (
            (Result<IReadOnlyList<(string, string, string)>, SqlError>.Ok<
                IReadOnlyList<(string, string, string)>,
                SqlError
            >)joinResult
        ).Value;
        Assert.Equal(3, appointments.Count);
        Assert.All(appointments, a => Assert.Equal("Diana Prince", a.Name));
        Assert.Equal("2026-01-15", appointments[0].Date);
        Assert.Equal("Annual checkup", appointments[0].Notes);
        Assert.Equal("2026-03-10", appointments[2].Date);

        // Query with JOIN: patient + medications
        var medResult = _connection.Query<(string Name, string Drug, string Dosage)>(
            sql: """
            SELECT p.Name, m.DrugName, m.Dosage
            FROM Patients p
            INNER JOIN Medications m ON p.Id = m.PatientId
            ORDER BY m.DrugName
            """,
            mapper: r => (r.GetString(0), r.GetString(1), r.GetString(2))
        );
        Assert.IsType<Result<IReadOnlyList<(string, string, string)>, SqlError>.Ok<
            IReadOnlyList<(string, string, string)>,
            SqlError
        >>(medResult);
        var meds = (
            (Result<IReadOnlyList<(string, string, string)>, SqlError>.Ok<
                IReadOnlyList<(string, string, string)>,
                SqlError
            >)medResult
        ).Value;
        Assert.Equal(2, meds.Count);
        Assert.Equal("Aspirin", meds[0].Drug);
        Assert.Equal("Metformin", meds[1].Drug);

        // Aggregate: count appointments per patient
        var aptCount = _connection.Scalar<long>(
            sql: "SELECT COUNT(*) FROM Appointments WHERE PatientId = @pid",
            parameters: [new SqliteParameter("@pid", patientId)]
        );
        Assert.Equal(3L, ((Result<long?, SqlError>.Ok<long?, SqlError>)aptCount).Value);

        // Delete appointment and verify cascade-like behavior
        _connection.Execute(
            sql: "DELETE FROM Appointments WHERE Id = @id",
            parameters: [new SqliteParameter("@id", apt1Id)]
        );
        var remainingApts = _connection.Scalar<long>(
            sql: "SELECT COUNT(*) FROM Appointments WHERE PatientId = @pid",
            parameters: [new SqliteParameter("@pid", patientId)]
        );
        Assert.Equal(2L, ((Result<long?, SqlError>.Ok<long?, SqlError>)remainingApts).Value);
    }

    [Fact]
    public void ParameterizedQueryWorkflow_VariousTypes_AllParameterTypesWork()
    {
        // Insert data with various SQLite types
        var id = Guid.NewGuid().ToString();
        _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age, Email, IsActive) VALUES (@id, @name, @age, @email, @active)",
            parameters:
            [
                new SqliteParameter("@id", id),
                new SqliteParameter("@name", "Test O'Brien"),
                new SqliteParameter("@age", 25),
                new SqliteParameter("@email", "test@example.com"),
                new SqliteParameter("@active", 1),
            ]
        );

        // Query with string parameter containing special characters
        var result = _connection.Query<string>(
            sql: "SELECT Name FROM Patients WHERE Name = @name",
            parameters: [new SqliteParameter("@name", "Test O'Brien")],
            mapper: r => r.GetString(0)
        );
        Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError>>(
            result
        );
        var names = (
            (Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError>)result
        ).Value;
        Assert.Single(names);
        Assert.Equal("Test O'Brien", names[0]);

        // Query with integer parameter
        var ageResult = _connection.Query<int>(
            sql: "SELECT Age FROM Patients WHERE Age >= @minAge AND Age <= @maxAge",
            parameters: [new SqliteParameter("@minAge", 20), new SqliteParameter("@maxAge", 30)],
            mapper: r => r.GetInt32(0)
        );
        Assert.IsType<Result<IReadOnlyList<int>, SqlError>.Ok<IReadOnlyList<int>, SqlError>>(
            ageResult
        );
        Assert.Single(
            ((Result<IReadOnlyList<int>, SqlError>.Ok<IReadOnlyList<int>, SqlError>)ageResult).Value
        );

        // Query with LIKE parameter
        var likeResult = _connection.Query<string>(
            sql: "SELECT Name FROM Patients WHERE Name LIKE @pattern",
            parameters: [new SqliteParameter("@pattern", "%O'Brien%")],
            mapper: r => r.GetString(0)
        );
        Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Ok<IReadOnlyList<string>, SqlError>>(
            likeResult
        );
        Assert.Single(
            (
                (Result<IReadOnlyList<string>, SqlError>.Ok<
                    IReadOnlyList<string>,
                    SqlError
                >)likeResult
            ).Value
        );

        // Scalar with aggregate and parameter
        var avgAge = _connection.Scalar<double>(
            sql: "SELECT AVG(CAST(Age AS REAL)) FROM Patients WHERE IsActive = @active",
            parameters: [new SqliteParameter("@active", 1)]
        );
        Assert.IsType<Result<double?, SqlError>.Ok<double?, SqlError>>(avgAge);
        Assert.Equal(25.0, ((Result<double?, SqlError>.Ok<double?, SqlError>)avgAge).Value);
    }

    [Fact]
    public void ErrorHandling_InvalidSqlAndConstraintViolations_ReturnsErrors()
    {
        // Invalid SQL returns error
        var badQuery = _connection.Query<string>(
            sql: "SELECT FROM NONEXISTENT_TABLE WHERE",
            mapper: r => r.GetString(0)
        );
        Assert.IsType<Result<IReadOnlyList<string>, SqlError>.Error<
            IReadOnlyList<string>,
            SqlError
        >>(badQuery);

        // Constraint violation: duplicate primary key
        var id = Guid.NewGuid().ToString();
        _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age) VALUES (@id, 'First', 30)",
            parameters: [new SqliteParameter("@id", id)]
        );
        var duplicate = _connection.Execute(
            sql: "INSERT INTO Patients (Id, Name, Age) VALUES (@id, 'Second', 40)",
            parameters: [new SqliteParameter("@id", id)]
        );
        Assert.IsType<IntError>(duplicate);
        var error = ((IntError)duplicate).Value;
        Assert.NotNull(error.Message);
        Assert.NotEmpty(error.Message);

        // Invalid scalar returns error
        var badScalar = _connection.Scalar<long>(sql: "SELECT * FROM NOWHERE");
        Assert.IsType<Result<long?, SqlError>.Error<long?, SqlError>>(badScalar);

        // Invalid execute returns error
        var badExec = _connection.Execute(sql: "DROP TABLE IMAGINARY_TABLE");
        Assert.IsType<IntError>(badExec);
    }
}

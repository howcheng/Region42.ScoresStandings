# Region42.ScoresStandings.Application.Tests

Unit tests for the Application layer services using xUnit, Moq, and FluentAssertions.

## Test Structure

```
tests/
└── Region42.ScoresStandings.Application.Tests/
	├── Services/
	│   └── CsvImportServiceTests.cs      (CSV import validation and filtering)
	├── Helpers/
	│   └── TestDataBuilder.cs            (Test data factory methods)
	└── Region42.ScoresStandings.Application.Tests.csproj
```

## Testing Frameworks

### xUnit (Test Framework)
- Test runner and assertion library
- `[Fact]` - Single test case
- `[Theory]` - Parameterized test case with `[InlineData]`

### Moq (Mocking Framework)
- Mock repository dependencies
- Setup method behavior
- Verify method calls

### FluentAssertions (Assertion Library)
- Readable, fluent assertion syntax
- Better error messages than standard assertions

## Test Categories

### CsvImportServiceTests

**CSV Parsing Tests**
- ✅ Empty CSV handling
- ✅ Header-only CSV handling

**Filtering Tests**
- ✅ Practice rows filtered out
- ✅ 16U rows filtered out (not in scope)
- ✅ 10U/12U/14U game rows processed
- ✅ Board member rows filtered out

**Validation Tests**
- ✅ Missing home team error
- ✅ Missing away team error
- ✅ Same home/away team error
- ✅ Invalid date format error
- ✅ Multiple errors collected

**Parsing Tests**
- ✅ Age group extraction (10U, 12U, 14U)
- ✅ Gender extraction (Boys, Girls)

**Season Validation**
- ✅ Invalid season ID error

## Running Tests

### Visual Studio
1. Open Test Explorer: `Test > Test Explorer`
2. Click "Run All" or right-click to run specific tests
3. View results in Test Explorer window

### Command Line
```powershell
# Run all tests
dotnet test

# Run with verbose output
dotnet test --verbosity normal

# Run specific test
dotnet test --filter "FullyQualifiedName~CsvImportServiceTests.ValidateCsvAsync_WithPracticeRows_SkipsThem"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~CsvImportServiceTests"

# Generate code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### From Solution Root
```powershell
cd C:\Users\howard\source\repos\howcheng\Region42.ScoresStandings
dotnet test tests/Region42.ScoresStandings.Application.Tests/Region42.ScoresStandings.Application.Tests.csproj
```

## Test Data Helpers

### TestDataBuilder
Factory methods for creating test entities:

```csharp
// Create test entities
var season = TestDataBuilder.CreateSeason(id: 1, name: "Fall 2025");
var division = TestDataBuilder.CreateDivision(id: 1, seasonId: 1, AgeGroup.U12, Gender.Boys);
var team = TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "12UB01");
var game = TestDataBuilder.CreateGame(id: 1, homeTeamId: 1, awayTeamId: 2);

// Create CSV test data
var csvContent = TestDataBuilder.CreateCsvContent(
	CsvRowData.CreateGameRow(AgeGroup.U12, Gender.Boys, "12UB01", "12UB02"),
	CsvRowData.CreatePracticeRow("12UG03")
);
```

## Writing New Tests

### Basic Test Pattern

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
	// Arrange - Setup test data and mocks
	var mockRepository = new Mock<IRepository<Entity>>();
	mockRepository.Setup(r => r.GetByIdAsync(1))
		.ReturnsAsync(new Entity { Id = 1 });

	var service = new MyService(mockRepository.Object);

	// Act - Execute the method being tested
	var result = await service.DoSomethingAsync(1);

	// Assert - Verify the result
	result.Should().NotBeNull();
	result.Id.Should().Be(1);

	// Verify mock interactions (optional)
	mockRepository.Verify(r => r.GetByIdAsync(1), Times.Once);
}
```

### Using FluentAssertions

```csharp
// Basic assertions
result.Should().NotBeNull();
result.Should().Be(expectedValue);
result.Should().BeTrue();
result.Should().BeFalse();

// String assertions
result.Should().Contain("substring");
result.Should().StartWith("prefix");
result.Should().EndWith("suffix");
result.Should().BeEquivalentTo("expected");

// Collection assertions
list.Should().NotBeEmpty();
list.Should().HaveCount(5);
list.Should().Contain(item => item.Name == "Test");
list.Should().BeEquivalentTo(expectedList);

// Exception assertions
var act = () => service.ThrowException();
act.Should().Throw<ArgumentException>()
	.WithMessage("*parameter*");
```

## Test Coverage Goals

### Current Coverage
- ✅ CsvImportService validation logic
- ✅ Filtering rules (Games, 10U/12U/14U)
- ✅ Error collection and reporting

### To Be Added
- [ ] StandingsService calculations
- [ ] TeamService CRUD operations
- [ ] GameService CRUD operations
- [ ] ScoreService score entry/updates
- [ ] VolunteerPointsService point tracking

## Continuous Integration

Tests will be run automatically on:
- Every commit to main branch
- Every pull request
- Scheduled nightly builds

## Best Practices

1. **One Assert Per Test** (when possible)
   - Makes failures easier to diagnose
   - Use multiple tests for multiple scenarios

2. **Descriptive Test Names**
   - Format: `MethodName_Scenario_ExpectedBehavior`
   - Example: `ValidateCsvAsync_WithPracticeRows_SkipsThem`

3. **Arrange-Act-Assert Pattern**
   - Clear separation of setup, execution, verification
   - Use comments to mark each section

4. **Mock External Dependencies**
   - Always mock repositories
   - Mock ILogger to avoid console noise
   - Focus tests on the unit being tested

5. **Use Test Data Builders**
   - Reusable test data creation
   - Default values for unimportant properties
   - Override only what matters for the test

6. **Async All The Way**
   - Test async methods with `async Task`
   - Use `await` for assertions
   - xUnit handles async tests natively

## Troubleshooting

### Tests Not Appearing in Test Explorer
1. Build the solution: `Ctrl+Shift+B`
2. Refresh Test Explorer
3. Check Output window for errors

### Moq Setup Not Working
```csharp
// Wrong - will throw
mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
	.Returns(new Entity()); // Missing Task wrapper

// Correct
mockRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
	.ReturnsAsync(new Entity());
```

### FluentAssertions Not Found
Make sure you have `using FluentAssertions;` at the top of your test file.

## References

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [Unit Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

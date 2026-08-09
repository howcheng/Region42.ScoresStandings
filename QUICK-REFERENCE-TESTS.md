# Quick Reference: Test Project

## 📁 Structure
```
tests/Region42.ScoresStandings.Application.Tests/
├── Services/CsvImportServiceTests.cs    (16 tests ✅)
├── Helpers/TestDataBuilder.cs           (test data factory)
└── README.md                            (full documentation)
```

## ▶️ Run Tests

### Command Line (Fastest)
```powershell
dotnet test                                    # All tests
dotnet test --verbosity normal                 # Show test names
dotnet watch test                              # Auto-run on changes
```

### Visual Studio
1. `Ctrl+Shift+B` (rebuild)
2. `Ctrl+E, T` (Test Explorer)
3. Click "Run All"

## ✅ Test Status

**All 16 tests passing** in 1.3 seconds

### Coverage
- ✅ CSV parsing (empty, header-only)
- ✅ Filtering (practices, 16U, board members)
- ✅ Age groups (10U, 12U, 14U process; 16U skips)
- ✅ Validation (missing teams, invalid dates, multiple errors)
- ✅ Gender parsing (Boys, Girls)

## 📦 Packages
- **xUnit** 3.1.4 - Test framework
- **Moq** 4.20.72 - Mocking
- **FluentAssertions** 8.10.0 - Assertions

## 🔧 Test Helpers

### Create Test Data
```csharp
var season = TestDataBuilder.CreateSeason();
var division = TestDataBuilder.CreateDivision(AgeGroup.U12, Gender.Boys);
var team = TestDataBuilder.CreateTeam(name: "12UB01");
```

### Create CSV Test Data
```csharp
var csv = TestDataBuilder.CreateCsvContent(
	CsvRowData.CreateGameRow(AgeGroup.U12, Gender.Boys, "12UB01", "12UB02"),
	CsvRowData.CreatePracticeRow("12UG03")  // Will be filtered
);
```

## 📖 Test Pattern
```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedBehavior()
{
	// Arrange
	_mockRepository.Setup(r => r.GetByIdAsync(1))
		.ReturnsAsync(new Entity { Id = 1 });

	// Act
	var result = await _service.DoSomething(1);

	// Assert
	result.Should().NotBeNull();
	result.Id.Should().Be(1);
}
```

## 🎯 Next Steps
- ✅ Tests infrastructure ready
- ⏳ Add tests for StandingsService (Step 18)
- ⏳ Add tests for TeamService (Step 19)
- ⏳ Add tests for GameService (Step 19)
- ⏳ Add tests for ScoreService (Step 20)

## 📚 Full Documentation
See `tests/Region42.ScoresStandings.Application.Tests/README.md` for:
- Complete test examples
- FluentAssertions guide
- Troubleshooting
- Best practices

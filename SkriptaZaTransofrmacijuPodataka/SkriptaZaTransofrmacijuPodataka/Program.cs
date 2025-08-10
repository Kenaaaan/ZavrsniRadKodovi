using Newtonsoft.Json;
using System.ComponentModel;
using System.Xml;
using ClosedXML.Excel;

try
{
    var filePath = @"C:\Users\kenan\OneDrive\Desktop\stanovnistvoData.xlsx";

    Console.WriteLine("Učitavanje Excel fajla i transformacija podataka...");
    
    var lokacijeDict = new Dictionary<string, List<Age>>();

    using (var workbook = new XLWorkbook(filePath))
    {
        var worksheet = workbook.Worksheet(1);
        Console.WriteLine($"Učitan worksheet: {worksheet.Name}");

        int brojRedova = worksheet.LastRowUsed().RowNumber();
        Console.WriteLine($"Pronađeno {brojRedova} redova u Excel fajlu.");

        Console.WriteLine("Analiza headerova tabele:");
        for (int col = 1; col <= 20; col++) 
        {
            Console.WriteLine($"Kolona {col}: '{worksheet.Cell(6, col).GetString()}'");
        }

        Console.WriteLine("\nDetaljna analiza ključnih redova:");
        for (int row = 7; row <= Math.Min(brojRedova, 20); row++)
        {
            var cell = worksheet.Cell(row, 2);
            string cellValue = cell.GetString();
            bool isEmpty = string.IsNullOrEmpty(cellValue);
            bool isWhiteSpace = string.IsNullOrWhiteSpace(cellValue);
            
            Console.WriteLine($"Red {row}: B='{cellValue}', IsEmpty={isEmpty}, IsWhiteSpace={isWhiteSpace}, CellType={cell.DataType}");
        }

        for (int row = 7; row <= brojRedova; row += 3) 
        {
            string lokacija = worksheet.Cell(row, 2).GetString().Trim();
            string starost = worksheet.Cell(row, 3).GetString().Trim();
            string pol = worksheet.Cell(row, 4).GetString().Trim();
            string ukupno = worksheet.Cell(row, 5).GetString().Trim();
            
            Console.WriteLine($"Red {row}: B='{lokacija}', C='{starost}', D='{pol}', E='{ukupno}'");

            if (string.IsNullOrWhiteSpace(lokacija))
            {
                Console.WriteLine($"Red {row}: Preskočen - nema lokacije u koloni B.");
                continue;
            }

            if (!string.IsNullOrWhiteSpace(lokacija) && !string.IsNullOrWhiteSpace(starost))
            {
                if (!string.IsNullOrWhiteSpace(starost) && pol.Contains("Ukupno") && !string.IsNullOrWhiteSpace(ukupno))
                {
                    Age ageData = ProcessRow(row, lokacija, starost, pol, ukupno, worksheet);
                    if (ageData != null)
                    {
                        if (!lokacijeDict.ContainsKey(lokacija))
                        {
                            lokacijeDict[lokacija] = new List<Age>();
                        }
                        lokacijeDict[lokacija].Add(ageData);
                        Console.WriteLine($"Red {row}: Dodata starosna grupa {ageData.age_label} za lokaciju {lokacija}");
                    }
                }
                else
                {
                    Console.WriteLine($"Red {row}: Preskočena obrada podataka - nije 'Ukupno' red ili nedostaju podaci.");
                }
            }
        }
    }

    var lokacije = lokacijeDict
        .Select(kvp => new Lokacija { area = kvp.Key, ages = kvp.Value })
        .ToList();

    if (lokacije.Count == 0)
    {
        Console.WriteLine("Upozorenje: Nema pronađenih podataka za lokacije.");
    }
    else
    {
        Console.WriteLine($"Pronađeno {lokacije.Count} lokacija sa podacima.");
        foreach (var lokacija in lokacije)
        {
            Console.WriteLine($"Lokacija: {lokacija.area}, Broj starosnih grupa: {lokacija.ages.Count}");
        }

        string desktopPath = @"C:\Users\kenan\OneDrive\Desktop";
        string outputFileName = Path.Combine(desktopPath, "stanovnistvo.json");
        string jsonContent = JsonConvert.SerializeObject(lokacije, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(outputFileName, jsonContent);
        Console.WriteLine($"Podaci su uspješno sačuvani u fajl: {outputFileName}");
        
        string jsonPreview = jsonContent.Length > 1000 ? jsonContent.Substring(0, 1000) + "..." : jsonContent;
        Console.WriteLine("Preview JSON-a:");
        Console.WriteLine(jsonPreview);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Greška: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}

Console.WriteLine("Pritisnite bilo koju tipku za izlaz...");
Console.ReadKey();

Age ProcessRow(int row, string lokacija, string starost, string pol, string ukupno, IXLWorksheet worksheet)
{
    Console.WriteLine($"Red {row}: Procesiranje - Lokacija='{lokacija}', Starost='{starost}', Pol='{pol}', Ukupno='{ukupno}'");
    
    string starosnaOznaka = starost;
    int godina;
    
    if (int.TryParse(starost, out godina))
    {
    }
    else if (starost.Contains("-"))
    {
        string[] starosniRaspon = starost.Split('-');
        if (starosniRaspon.Length > 0 && int.TryParse(starosniRaspon[0], out godina))
        {
        }
        else
        {
            Console.WriteLine($"Red {row}: Upozorenje - nevažeća starost: '{starost}', preskačem.");
            return null;
        }
    }
    else
    {
        Console.WriteLine($"Red {row}: Upozorenje - nevažeća starost: '{starost}', preskačem.");
        return null;
    }

    if (!int.TryParse(ukupno, out int ukupnoVrednost))
    {
        Console.WriteLine($"Red {row}: Upozorenje - nevažeća ukupna vrednost: '{ukupno}', preskačem.");
        return null;
    }

    var ageData = new Age
    {
        age = godina,
        age_label = starosnaOznaka,  
        total = ukupnoVrednost,
        education = new Education
        {
            not_attending = ParseCellValue(worksheet.Cell(row, 6).GetString()),
            preschool = ParseCellValue(worksheet.Cell(row, 7).GetString()),
            primary = ParseCellValue(worksheet.Cell(row, 8).GetString()),
            secondary = ParseCellValue(worksheet.Cell(row, 9).GetString()),
            post_secondary = ParseCellValue(worksheet.Cell(row, 10).GetString()),
            higher = ParseCellValue(worksheet.Cell(row, 11).GetString()),
            basic_academic = ParseCellValue(worksheet.Cell(row, 12).GetString()),
            specialist = ParseCellValue(worksheet.Cell(row, 13).GetString()),
            masters = ParseCellValue(worksheet.Cell(row, 14).GetString()),
            doctoral = ParseCellValue(worksheet.Cell(row, 15).GetString()),
            first_cycle = ParseCellValue(worksheet.Cell(row, 16).GetString()),
            second_cycle = ParseCellValue(worksheet.Cell(row, 17).GetString()),
            integrated_cycle = ParseCellValue(worksheet.Cell(row, 18).GetString()),
            third_cycle = ParseCellValue(worksheet.Cell(row, 19).GetString())
        }
    };

    Console.WriteLine($"Red {row}: Education values: Not attending: {ageData.education.not_attending}, " +
                     $"Preschool: {ageData.education.preschool}, " +
                     $"Primary: {ageData.education.primary}, " +
                     $"Secondary: {ageData.education.secondary}");

    return ageData;
}

int ParseCellValue(string cellValue)
{
    if (string.IsNullOrWhiteSpace(cellValue) || cellValue == "-")
        return 0;

    if (int.TryParse(cellValue, out int result))
        return result;

    return 0;
}

public class Lokacija
{
    public string area { get; set; }
    public List<Age> ages { get; set; }
}

public class Age
{
    public int age { get; set; }
    public string age_label { get; set; } 
    public int total { get; set; }
    public Education education { get; set; }
}

public class Education
{
    public int not_attending { get; set; }
    public int preschool { get; set; }
    public int primary { get; set; }
    public int secondary { get; set; }
    public int post_secondary { get; set; }
    public int higher { get; set; }
    public int basic_academic { get; set; }
    public int specialist { get; set; }
    public int masters { get; set; }
    public int doctoral { get; set; }
    public int first_cycle { get; set; }
    public int second_cycle { get; set; }
    public int integrated_cycle { get; set; }
    public int third_cycle { get; set; }
}
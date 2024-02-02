using System.Text.RegularExpressions;

if (args.Length == 0)
{
    DumpHelp();
    return;
}

var countBytes = false;
var countChars = false;
var countWords = false;
var countLines = false;
var fileName = "../../../test.txt";

foreach (var arg in args)
{
    switch (arg)
    {
        case "-c":
            countBytes = true;
            break;
        case "-m":
            countChars = true;
            break;
        case "-w":
            countWords = true;
            break;
        case "-l":
            countLines = true;
            break;
        default:
            fileName = arg;
            break;
    }
}

if (fileName == "")
{
    Console.WriteLine("No file name provided");
    return;
}

if (!File.Exists(fileName))
{
    Console.WriteLine($"File {fileName} does not exist");
    return;
}

if (countBytes == false && countChars == false && countWords == false && countLines == false)
{
    countChars = true;
    countWords = true;
    countLines = true;
}

var fileText = File.ReadAllText(fileName);
var output = $" {fileName}";
if (countLines)
    output = $"\t{fileText.Split('\n').Length} {output}";
if (countWords)
    output = $"\t{Regex.Matches(fileText, @"\p{L}+").Count} {output}";
if (countBytes)
    output = $"\t{new FileInfo(fileName).Length} {output}";
if (countChars)
    output = $"\t{fileText.Length} {output}";
Console.WriteLine(output);

return;

void DumpHelp()
{
    Console.WriteLine("Usage: WordCount [-c] [-w] [-l] <filename>");
    Console.WriteLine("  -c: show bytes in file");
    Console.WriteLine("  -w: show word count");
    Console.WriteLine("  -l: show line count");
    Console.WriteLine("  -m: show character count");
    Console.WriteLine("default of filename only shows character, word and line count");
}
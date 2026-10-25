using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Spectre.Console;

// Dumb ini reading thing
[DllImport("kernel32", CharSet = CharSet.Unicode)]
static extern int GetPrivateProfileString(string Section, string Key, string Default, StringBuilder RetVal, int Size, string FilePath);

#region "Constants" and stuff

string HighscoreTablePath = "C:/Users/"+Environment.UserName+"/AppData/Local/VIVIDSTASIS/highscore_table";
string SongInformationPath = "C:/Program Files (x86)/Steam/steamapps/common/vividstasis/song_information.bin";
int StartIndex = 1;
int PageSize = 30;
Modes Mode = Modes.Rate;

// Good and awesome non-repetitive code
if (File.Exists("highscorepath.txt"))
{
    using (StreamReader sr = new StreamReader("highscorepath.txt"))
    {
        HighscoreTablePath = sr.ReadToEnd();
        //AnsiConsole.MarkupLineInterpolated($"[italic]Using highscore table at {HighscoreTablePath}[/]");
    }
}
if (File.Exists("songinformationpath.txt"))
{
    using (StreamReader sr = new StreamReader("songinformationpath.txt"))
    {
        SongInformationPath = sr.ReadToEnd();
        //AnsiConsole.MarkupLineInterpolated($"[italic]Using song_information at {SongInformationPath}[/]");
    }
}
if (!File.Exists(HighscoreTablePath))
{
    AnsiConsole.MarkupLineInterpolated($"""
    [red][bold]Could not find[/] highscore table file at [italic]{HighscoreTablePath}[/]![/]
    You can change the path this program looks for by creating a file named [italic]highscorepath.txt[/] next to this program with the filepath inside of it.
    If you're having trouble finding it, this file should be located in your [italic]save folder[/].
    """);
    return;
}
if (!File.Exists(SongInformationPath))
{
    AnsiConsole.MarkupLineInterpolated($"""
    [red][bold]Could not find[/] song_information.bin at [italic]{SongInformationPath}[/]![/]
    You can change the path this program looks for by creating a file named [italic]songinformationpath.txt[/] next to this program with the filepath inside of it.
    If you're having trouble finding it, this file should be located in [italic]the same folder as the game .exe[/].
    """);
    return;
}

//"%localappdata%/VIVIDSTASIS/highscore_table"
//const string HighscoreTablePath = "C:/Users/"+Environment.UserName+"/OneDrive/Desktop/vividstasis speedrunning/practice saves/uhhh like postgame save ig/highscore_table";

#endregion

#region song_information.bin

// Generate song information off song_information.bin
Dictionary<int, SongData> Songs = [];

using (FileStream file = new FileStream(SongInformationPath, FileMode.Open, FileAccess.Read))
{
    file.Seek(5, SeekOrigin.Begin); // skip header

    while (true) // true?
    {
        int NextByte = file.ReadByte();
        if (NextByte != 0xA0) break;
        
        int TempID = -1;
        string TempName = "";
        List<float> TempConstants = [];
        bool TempHasEncore = false;

        // readSong()
        while (true)
        {
            NextByte = file.ReadByte();

            if (NextByte == 0xA1) break; // need to not use a switch here to break out of while
            switch (NextByte)
            {
                case 0xA2:
                    //Console.WriteLine("A2");

                    // TODO see if you can just do switch (file.readbyte(), file.readbyte()) instead of doing this (like probably)
                    int Byte1 = file.ReadByte();
                    int Byte2 = file.ReadByte();

                    switch (Byte1, Byte2)
                    {
                        // B2 00 = song id (32bit integer)
                        case (0xB2, 0x00):
                            //Console.WriteLine("B2 00");
                            // This is the Song ID. Read 4 bytes then convert it to an int32
                            byte[] idbytes = [0x00,0x00,0x00,0x00];
                            file.ReadExactly(idbytes, 0, 4);
                            TempID = BitConverter.ToInt32(idbytes);
                            break;
                        // B8 01 = song name (string)
                        case (0xB8, 0x01):
                            //Console.WriteLine("B8 01");
                            // This is the name of the song. Keep reading bytes until we reach 0x00.
                            while (true)
                            {
                                char nextchar = (char)file.ReadByte();
                                if (nextchar == 0x00) break;
                                TempName += nextchar;
                            }
                            break;
                        // B7 07 = has encore (1 byte bool)
                        case (0xB7, 0x07):
                            TempHasEncore = (file.ReadByte() == 1);
                            break;
                    }

                    break;
                case 0xC0:
                    // First thing here is a string that we don't care about, skip to 0x00
                    while (true)
                    {
                        int nextbyte = file.ReadByte();
                        if (nextbyte == 0x00) break;
                    }

                    // Now for the thing we do care about, this is the chart constant as a 32-bit float
                    byte[] ccbytes = [0x00,0x00,0x00,0x00];
                    file.ReadExactly(ccbytes, 0, 4);
                    //Console.WriteLine("new diff on "+TempID+" "+BitConverter.ToSingle(ccbytes));
                    TempConstants.Add(BitConverter.ToSingle(ccbytes));

                    // Useless string 2
                    while (true)
                    {
                        int nextbyte = file.ReadByte();
                        if (nextbyte == 0x00) break;
                    }

                    break;
            }
        }

        //if (TempID < 0 || string.IsNullOrWhiteSpace(TempName)) throw new FileLoadException("Tried to load song with ID "+TempID, "song_information.bin");

        Songs[TempID] = new SongData
        {
            Name = TempName,
            Constants = TempConstants,
            HasEncore = TempHasEncore,
            Scores = []
        };
    }
}

#endregion

#region Collecting scores + completion data

//, "PRELUDE"
string[] DiffList = ["OPENING", "MIDDLE", "FINALE", "ENCORE"];
List<ScoreData> Scores = [];

/*
TODO
- [..should be fixed already lol] Either make ScoreData.Print work with MarkupLineInterpolated or properly escape square brackets in song names
- [done??] ACTUALLY color-code rates. like SERIOUSLY
- Fix w and e in print loop (they just do nothing)
- Alleged spacing issues with CCs <10 but i think I fixed that second one
- Preludes??????
- Add EX ratings and Completion tabs? (After)
*/

for (int id = 3; id <= 206; id++)
{
    if (id == 131 || id == 200) continue; // acta est fabula, plaudite & SUPERNOVA ~Life Cycle of a Star~

    SongData Song;
    try
    {
        Song = Songs[id];
        //Console.WriteLine(id +" "+ Song.Name);
    }
    catch (KeyNotFoundException) // Song IDs 0, 2, 145 and 185 don't exist
    {
        //Console.WriteLine($"[Song with ID {id} not found]");
        continue;
    }

    // Check each difficulty of each song
    for (int j = 0; j < DiffList.Length; j++)
    {
        string DiffName = DiffList[j];

        StringBuilder SongScore = new StringBuilder(255);
        StringBuilder SongFCAC  = new StringBuilder(255);
        _ = GetPrivateProfileString(id.ToString(), DiffName         , "", SongScore, 255, HighscoreTablePath);
        _ = GetPrivateProfileString(id.ToString(), DiffName+"_COMBO", "", SongFCAC , 255, HighscoreTablePath);

        int fcac;
        try
        {
            fcac = (int)float.Parse(SongFCAC.ToString());
        }
        catch (FormatException)
        {
            fcac = 0;
        }

        if (
            string.IsNullOrWhiteSpace(SongScore.ToString()) ||
            double.Parse(SongScore.ToString()) < 600000 ||
            //string.IsNullOrWhiteSpace(SongFCAC.ToString()) ||
            j > Song.Constants.Count
        ) continue;

        Song.Scores.Add(j, float.Parse(SongScore.ToString()));

        Scores.Add(new ScoreData
        {
            SongID = id,
            SongName = Song.Name,
            Difficulty = DiffName,
            Constant = Song.Constants[j],
            Score = float.Parse(SongScore.ToString()),
            Rate = GetScoreRate(double.Parse(SongScore.ToString()), Song.Constants[j], fcac),
            FCAC = fcac
        });
    }

    // Completion time

    float compSongTotalRate = 0;
    int compDivider = Song.HasEncore ? 4 : 3;
    int compDiffToCheck = compDivider - 1;
    float compMaxRate = 0;

    while (compDiffToCheck >= 0)
    {
        float score;
        try { score = Song.Scores[compDiffToCheck]; }
        catch (KeyNotFoundException) { score = 0; }

        float rate = score switch
        {
            >= 710000 => (float)(Math.Pow((score - 710000) / 20000, 1.7005483074552052) / compDivider),
            _         => 0
        };

        compMaxRate = MathF.Max(compMaxRate, rate);
        compSongTotalRate += compMaxRate;
        compDiffToCheck--;
    }

    Song.Completion = compSongTotalRate;
    Songs[id] = Song;
}

#endregion

Scores.Sort((y, x) => x.Rate.CompareTo(y.Rate));

List<int> BullshitCompletionSorting = [];
for (int i = 3; i <= 206; i++) {if (Songs.ContainsKey(i) && i != 131 && i != 200) BullshitCompletionSorting.Add(i);}
BullshitCompletionSorting.Sort((y, x) => Songs[x].Completion.CompareTo(Songs[y].Completion));

#region Print loop

// Main print loop of actually displaying entries
while (true)
{
    switch (Mode)
    {
        case Modes.Rate:
            PrintRateEntries();
            break;
        case Modes.EX:
            PrintEXEntries();
            break;
        case Modes.Completion:
            PrintCompletionEntries();
            break;
    }
    AnsiConsole.MarkupLine("""

    Type [bold]b[/] then a number N to show the top N entries. [italic]Example: b30[/]
    Type [bold]#[/] then a number N to navigate to that spot. [italic]Example: #60[/]
    Type [bold]p[/] then a number N to change the size of one page. [italic]Example: p10[/]
    Type [bold]w[/] to go back one page. Type [bold]e[/] to go forward one page.

    Type [bold]z[/] to switch to regular rating view.
    Type [bold]c[/] to switch to Completion view.
    [italic](EX Rating view coming soon, hopefully)[/]

    Type [bold].[/] or hit [bold]Ctrl+C[/] to exit.
    """);
    //Type [bold]x[/] to switch to EX rating view.

    string? Input = Console.ReadLine();
    if (Input is null) break;
    int num;
    try
    {
        num = int.Parse(Input[1..^0]);
    }
    catch
    {
        num = -1;
    }
    switch (char.ToLower(Input[0]))
    {
        case 'b':
            StartIndex = 1;
            PageSize = num>0 ? num : 30;
            break;
        case '#':
            StartIndex = num>0 ? num : 1;
            break;
        case 'p':
            PageSize = num>0 ? num : 10;
            break;
        
        case 'w':
            StartIndex -= PageSize;
            StartIndex = Math.Max(StartIndex, 1);
            break;
        case 'e':
            StartIndex += PageSize;
            break;

        case 'z':
            Mode = Modes.Rate;
            Scores.Sort((y, x) => x.Rate.CompareTo(y.Rate));
            break;
        /*
        case 'x':
            Mode = Modes.EX;
            Scores.Sort((y, x) => x.EXRate.CompareTo(y.EXRate));
            throw new NotImplementedException("Fuck");
        */
        case 'c':
            Mode = Modes.Completion;
            break;
        
        case '.':
            return;
    }
}

#endregion

#region Functions & structs

double GetScoreRate(double score, float cc, int fcac)
{
    cc = MathF.Round(cc, 1);

    double r_value = score switch
    {
        <  600000  => 0,
        >= 1008000 => cc + 2.8,
        >= 1000000 => ((score - 1000000) / 10000) + cc + 2,
        >= 980000  => ((score -  980000) / 20000) + cc + 1,
        >= 950000  => ((score -  950000) / 60000) + cc + 0.5,
        _          => ((score -  950000) / 50000) + cc + 0.5
    };
    
    return Math.Max(r_value, 0) + (fcac * 0.1);
}

/*
function get_ex_rating_value(arg0, arg1)
{
    arg1 = round(arg1 * 10) / 10;
    var exr_max = 1;
    var exr_plat = ((arg0 - 0.99) * 2) + 0.98;
    var exr_gold = ((arg0 - 0.98) * 3) + 0.95;
    var exr_silv = ((arg0 - 0.96) * 5) + 0.85;
    var exr_bronz = ((arg0 - 0.93) * 5) + 0.7;
    var exr_good = ((arg0 - 0.9) * 6.666666666666667) + 0.5;
    var exr_okay = ((arg0 - 0.8) * 2.5) + 0.25;
    var exr = 0;
    
    if (arg0 >= 1)
        exr = exr_max;
    else if (arg0 >= 0.99)
        exr = exr_plat;
    else if (arg0 >= 0.98)
        exr = exr_gold;
    else if (arg0 >= 0.96)
        exr = exr_silv;
    else if (arg0 >= 0.93)
        exr = exr_bronz;
    else if (arg0 >= 0.9)
        exr = exr_good;
    else if (arg0 >= 0.8)
        exr = exr_okay;
    
    var tconst = round((arg1 / 9) * 100) / 10;
    return (tconst / 10) * exr;
}
*/

void PrintRateEntries()
{
    AnsiConsole.MarkupLine($"[underline] {"#".PadLeft((int)Math.Log10(StartIndex+PageSize-1))} Song Name                  Level     Score    CT. Rate  [/]");
    for (int p = StartIndex; p <= (StartIndex + PageSize - 1); p++)
    {
        try
        {
            // what
            string idx = p.ToString().PadLeft(1+(int)Math.Log10(StartIndex+PageSize-1));
            if (p%10 == 0) idx = "[underline]"+idx+"[/]";
            Scores[p-1].PrintRate(idx);
        }
        catch (ArgumentOutOfRangeException) // N/A
        {
            AnsiConsole.MarkupLine($"{p.ToString().PadLeft(2)} N/A                        [#808080]N/A           N/A[/]  -      N/A  ");
        }
    }
}

void PrintEXEntries()
{
    //todo
}

void PrintCompletionEntries()
{
    AnsiConsole.MarkupLine($"[underline] {"#".PadLeft((int)Math.Log10(StartIndex+PageSize-1))} Song Name                  [#76FF00]Opening[/]  [#00B2FF]Middle[/]   [#FF1E00]Finale[/]   [#B200FF]Encore[/]   Completion[/]");
    for (int p = StartIndex; p <= (StartIndex + PageSize - 1); p++)
    {
        // what
        string idx = p.ToString().PadLeft(1+(int)Math.Log10(StartIndex+PageSize-1));
        if (p%10 == 0) idx = "[underline]"+idx+"[/]";
        try
        {
            // these are bad variable names
            int q = BullshitCompletionSorting[p];
            SongData song = Songs[q];
            var d = song.Scores;

            float enc;
            try {
                if (song.HasEncore) enc = song.Scores[3];
                else enc = -1f;
            }
            catch (KeyNotFoundException) {enc = 0f;}

            float fin;
            try {fin = MathF.Max(GetScore(d,2),enc);}
            catch (KeyNotFoundException) {fin = 0f;}

            float mid;
            try {mid = MathF.Max(GetScore(d,1),fin);}
            catch (KeyNotFoundException) {mid = 0f;}

            float opn;
            try {opn = MathF.Max(GetScore(d,0),mid);}
            catch (KeyNotFoundException) {opn = 0f;}
            
            AnsiConsole.MarkupLine($"{idx} {Markup.Escape(Format.Name(q, song.Name))}  {Format.Score(opn,opn<=mid)}  {Format.Score(mid,mid<=fin)}  {Format.Score(fin,fin<=enc)}  {Format.Score(enc)}  {FormatCompletion(song.Completion)}");
        }
        catch (ArgumentOutOfRangeException)
        {
            AnsiConsole.MarkupLine($"{idx} N/A");
        }
        //*/
    }

    static float GetScore(Dictionary<int,float> scores, int diff)
    {
        if (scores.TryGetValue(diff, out float value))
        {
            return value;
        }
        return 0f;
    }

    static string FormatCompletion(float Completion)
    {
        string t = Math.Round(Completion, 3).ToString("0.000").PadLeft(6);
        return Completion switch
        {
            >= 98 => $"[#FF6537]{t[0]}[/][#FFFF34]{t[1]}[/][#65FF3D]{t[2]}[/][#00FFFF]{t[3]}[/][#3732FF]{t[4]}[/][#F229FF]{t[5]}[/]",
            >= 95 => $"[#FF8383]{t[0]}[/][#EDEE7B]{t[1]}[/][#ADFF87]{t[2]}[/][#42F5C0]{t[3]}[/][#8093FF]{t[4]}[/][#BC77F6]{t[5]}[/]",
            >= 90 => $"[#E1B9FA]{t[0]}[/][#B8B2F9]{t[1]}[/][#BCCCF9]{t[2]}[/][#BAE8FA]{t[3]}[/][#C8FCF3]{t[4]}[/][#CAF8C8]{t[5]}[/]",
            >= 75 => $"[#FF59AB]{t}[/]",
            //>= 14 => $"[#FFD200]{t}[/]",
            >= 66.666f => $"[#00FBFF]{t}[/]",
            //>= 93 => $"[#D8712D]{t}[/]",
            //>= 50 => $"[#F3D779]{t}[/]",
            >= 50 => $"[#B200FF]{t}[/]",
            >= 33.333f => $"[#00FF21]{t}[/]",
            >= 25 => $"[#0087E9]{t}[/]",
            _     => $"[#D8D8D8]{t}[/]"
        };
    }
}

struct SongData
{
    public string Name;
    public List<float> Constants;

    public bool HasEncore;
    public Dictionary<int, float> Scores; // completion :foxplush:
    public float Completion;
}

struct ScoreData
{
    public int SongID;
    public string SongName;
    public string Difficulty;
    public float Constant;
    public float Score;
    public double Rate;
    public int FCAC;
    public double EXRate;

    // oh my god
    internal readonly string DisplayRate
    {
        get
        {
            string t = Math.Round(Rate, 3).ToString("0.000").PadLeft(6);
            return Rate switch
            {
                >= 18 => $"[#FF6537]{t[0]}[/][#FFFF34]{t[1]}[/][#65FF3D]{t[2]}[/][#00FFFF]{t[3]}[/][#3732FF]{t[4]}[/][#F229FF]{t[5]}[/]",
                >= 17 => $"[#FF8383]{t[0]}[/][#EDEE7B]{t[1]}[/][#ADFF87]{t[2]}[/][#42F5C0]{t[3]}[/][#8093FF]{t[4]}[/][#BC77F6]{t[5]}[/]",
                >= 16 => $"[#E1B9FA]{t[0]}[/][#B8B2F9]{t[1]}[/][#BCCCF9]{t[2]}[/][#BAE8FA]{t[3]}[/][#C8FCF3]{t[4]}[/][#CAF8C8]{t[5]}[/]",
                >= 15 => $"[#FF59AB]{t}[/]",
                >= 14 => $"[#FFD200]{t}[/]",
                >= 13 => $"[#00FBFF]{t}[/]",
                >= 12 => $"[#D8712D]{t}[/]",
                >= 10 => $"[#F3D779]{t}[/]",
                >=  8 => $"[#B200FF]{t}[/]",
                >=  5 => $"[#00FF21]{t}[/]",
                >=  2 => $"[#0087E9]{t}[/]",
                _     => $"[#D8D8D8]{t}[/]"
            };
        }
    }

    internal readonly string DisplayFCAC => FCAC switch
    {
        1 => "[#00FFFF]FC[/]",
        2 => "[#FF00FF]AC[/]",
        _ => "- "
    };

    public void PrintRate(string idx)
    {
        // ..Backstage shenanigans
        if (Difficulty == "ENCORE")
        {  
            // Is this even a good way to do this probably not
            SongName = SongID switch
            {
                5 => "00",
                10 => "Aci-L",
                50 => "Goodbye To Your Vicious Cycles",
                83 => "DeSPAROLA",
                180 => "Scarlet Death",
                193 => "Era Chimaera",
                _ => SongName
            };
            switch (SongID)
            {
                case 5:
                case 6:
                case 10:
                case 17:
                case 50:
                case 83:
                case 135:
                case 180:
                case 193:
                    Difficulty = "BST";
                    break;
            };
        }

        AnsiConsole.MarkupLine($"{idx} {Markup.Escape(Format.Name(SongID, SongName))}  {Format.Diff(Difficulty)} {Constant.ToString("0.0").PadLeft(4)}[/]  {Format.Score(Score)}  {DisplayFCAC}  {DisplayRate}");
    }
}

class Format
{
    public static string Diff(string Difficulty)
    {
        string diff = Difficulty switch
        {
            "OPENING" => "OPN",
            _ => Difficulty[0..3]
        };
        string hex = diff switch
        {
            "OPN" => "76FF00",
            "MID" => "00B2FF",
            "FIN" => "FF1E00",
            "ENC" => "B200FF",
            "BST" => "FF00D8",
            _ => "FFFFFF"
        };
        return $"[#{hex}]{diff}";
    }

    public static string Name(int SongID, string SongName) => SongID switch
    {
        18  => "∀                        ",
        71  => "∀NARCHIC LIGHT           ",
        93  => "χΩ                       ",
        177 => "ULTiM∀TUM                ",
        _ => SongName.Length switch
        {
            >= 25 => SongName[0..22] + "...",
            _     => SongName.PadRight(25)
        }
    };

    public static string Score(float Score, bool dim = false)
    {
        if (Score < 0f || float.IsNaN(Score)) return "[#808080]    N/A[/]";
        int roundedscore = (int)MathF.Round(Score);
        string hex = roundedscore switch
        {
            >= 1010000 => "FFFFFF", // VS
            >= 1009000 => "FF006E", // V+
            >= 1008000 => "FF7FB6", // V
            >= 1004000 => "FF3EFF", // SS+
            >= 1000000 => "FF66C4", // SS
            >=  990000 => "E055FF", // S+
            >=  980000 => "A373FF", // S
            >=  950000 => "5BE1FF", // AA
            >=  900000 => "59FF7A", // A
            >=  850000 => "FFE858", // B
            >=  800000 => "FD8258", // C
            >=  600000 => "FD6567", // D
            _          => "808080"  // E
        };
        string str = $"[#{hex}]{roundedscore.ToString().PadLeft(7)}[/]";
        if (dim) str = "[dim][italic]"+str+"[/][/]";
        return str;
    }
}

enum Modes
{
    Rate,
    EX,
    Completion
}

#endregion
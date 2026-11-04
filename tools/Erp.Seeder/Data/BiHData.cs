namespace Erp.Seeder.Data;

internal sealed record City(string Name, string PostalCode);

/// <summary>A kind of goods sold in several variants, e.g. sizes; each variant is sold under several brands.</summary>
internal sealed record GoodsLine(string Name, string UnitCode, string[] Variants);

internal sealed record ServiceLine(string Name, string UnitCode);

/// <summary>
/// Word lists for Bosnian mock data. Brand and company names are made up from rivers and
/// mountains so no real business is imitated.
/// </summary>
internal static class BiHData
{
    public static readonly City[] Cities =
    [
        new("Sarajevo", "71000"),
        new("Banja Luka", "78000"),
        new("Tuzla", "75000"),
        new("Zenica", "72000"),
        new("Mostar", "88000"),
        new("Bihać", "77000"),
        new("Brčko", "76100"),
        new("Bijeljina", "76300"),
        new("Prijedor", "79101"),
        new("Trebinje", "89101"),
        new("Doboj", "74000"),
        new("Cazin", "77220"),
        new("Goražde", "73000"),
        new("Travnik", "72270"),
        new("Livno", "80101"),
        new("Konjic", "88400"),
        new("Gradačac", "76250"),
        new("Visoko", "71300"),
        new("Zvornik", "75400"),
        new("Široki Brijeg", "88220"),
    ];

    public static readonly string[] Streets =
    [
        "Ferhadija", "Maršala Tita", "Zmaja od Bosne", "Alipašina", "Džemala Bijedića", "Branilaca grada",
        "Kralja Tvrtka", "Bosanska", "Hasana Kikića", "Safvet-bega Bašagića", "Vuka Karadžića", "Njegoševa",
        "Bulevar cara Dušana", "Stjepana Radića", "Kneza Domagoja", "Trg slobode", "Mehmeda Spahe",
        "Bulevar Meše Selimovića", "Kralja Petra I", "Musala",
    ];

    public static readonly string[] FirstNames =
    [
        "Amra", "Emir", "Lejla", "Adnan", "Selma", "Haris", "Ajla", "Mirza", "Amela", "Dženan", "Nermina", "Tarik",
        "Edina", "Jasmin", "Belma", "Nikola", "Milica", "Dragan", "Jelena", "Marko", "Ivana", "Ante", "Marija", "Goran",
    ];

    public static readonly string[] LastNames =
    [
        "Hadžić", "Kovačević", "Hodžić", "Begić", "Delić", "Mehmedović", "Ibrahimović", "Šehić", "Jukić", "Marić",
        "Petrović", "Jovanović", "Babić", "Husić", "Alić", "Karić", "Softić", "Mujić", "Popović", "Tomić", "Perić",
        "Bašić", "Zukić", "Topić",
    ];

    public static readonly string[] Rivers =
    [
        "Bosna", "Drina", "Una", "Sana", "Vrbas", "Neretva", "Lašva", "Spreča", "Krivaja", "Rama",
        "Buna", "Miljacka", "Fojnica", "Željeznica", "Ukrina", "Tinja", "Trebišnjica", "Pliva", "Usora", "Drinjača",
    ];

    public static readonly string[] Activities =
    [
        "Promet", "Trade", "Commerce", "Distribucija", "Gradnja", "Trgovina", "Prevoz",
        "Inženjering", "Agro", "Metal", "Elektro", "Market", "Servis", "Export-Import",
    ];

    /// <summary>Sole proprietors: samostalna zanatska / trgovinska / ugostiteljska radnja.</summary>
    public static readonly string[] Crafts = ["SZR", "STR", "SUR"];

    public static readonly string[] Brands =
    [
        "Bjelašnica", "Igman", "Jahorina", "Treskavica", "Vlašić", "Kozara", "Romanija", "Prenj", "Čvrsnica", "Zelengora",
        "Majevica", "Ozren", "Vranica", "Trebević", "Velež", "Grmeč", "Klekovača", "Konjuh", "Javor", "Borja",
        "Zvijezda", "Kamešnica", "Raduša", "Cincar", "Lisina", "Motajica", "Plješevica", "Visočica", "Maglić", "Volujak",
    ];

    public static readonly (string Code, string Name)[] Units =
    [
        ("kom", "Komad"),
        ("kg", "Kilogram"),
        ("l", "Litar"),
        ("m", "Metar"),
        ("m2", "Kvadratni metar"),
        ("m3", "Kubni metar"),
        ("t", "Tona"),
        ("pak", "Paket"),
        ("sat", "Sat"),
    ];

    public static readonly GoodsLine[] Goods =
    [
        new("Vijak", "kom", ["M6x40", "M8x60", "M10x80"]),
        new("Matica", "kom", ["M6", "M8", "M10"]),
        new("Kafa mljevena", "kom", ["100 g", "200 g", "500 g"]),
        new("Brašno T-500", "kom", ["1 kg", "5 kg", "25 kg"]),
        new("Šećer kristal", "kom", ["1 kg", "5 kg"]),
        new("Ulje suncokretovo", "kom", ["1 l", "5 l"]),
        new("Mineralna voda", "kom", ["0,5 l", "1,5 l"]),
        new("Kabl PP-Y", "m", ["3x1,5", "3x2,5", "5x2,5"]),
        new("Cijev PVC", "m", ["Ø50", "Ø75", "Ø110"]),
        new("Cement", "kg", ["CEM II 42,5 R", "CEM I 52,5 N"]),
        new("Pijesak", "t", ["0-4 mm", "4-8 mm"]),
        new("Boja za zid", "l", ["bijela", "bež", "siva"]),
        new("Laminat", "m2", ["hrast 8 mm", "orah 10 mm"]),
        new("Keramičke pločice", "m2", ["30x30", "60x60"]),
        new("Daska jelova", "m3", ["24 mm", "48 mm"]),
        new("Papir A4", "pak", ["80 g/m²", "100 g/m²"]),
        new("Toner", "kom", ["crni", "plavi", "crveni", "žuti"]),
        new("Rukavice radne", "pak", ["S", "M", "L", "XL"]),
        new("Sijalica LED", "kom", ["9 W", "12 W", "15 W"]),
        new("Utičnica", "kom", ["jednostruka", "dvostruka"]),
        new("Prekidač", "kom", ["obični", "izmjenični"]),
        new("Ljepilo za pločice", "kg", ["C1", "C2TE"]),
        new("Silikon", "kom", ["bijeli", "providni"]),
        new("Deterdžent za veš", "kom", ["3 kg", "6 kg"]),
        new("Sok od jabuke", "kom", ["0,2 l", "1 l"]),
        new("Tjestenina", "kom", ["špageti", "penne", "fusilli"]),
        new("Mlijeko trajno", "l", ["2,8 %", "0,9 %"]),
        new("Čokolada", "kom", ["mliječna", "tamna", "s lješnjacima"]),
        new("Odvijač", "kom", ["PH1", "PH2", "ravni 5 mm"]),
        new("Bušilica", "kom", ["akumulatorska 18 V", "udarna 800 W"]),
        new("Brusna ploča", "kom", ["115 mm", "125 mm", "230 mm"]),
        new("Stiropor ploča", "m2", ["5 cm", "8 cm", "10 cm"]),
        new("Armaturna mreža", "kom", ["Q131", "Q188"]),
        new("Crijep", "kom", ["glineni", "betonski"]),
        new("Sredstvo za čišćenje", "l", ["podova", "stakla"]),
    ];

    public static readonly ServiceLine[] Services =
    [
        new("Usluga prevoza", "sat"),
        new("Montaža opreme", "sat"),
        new("Servis klima uređaja", "kom"),
        new("Održavanje računara", "sat"),
        new("Projektovanje", "sat"),
        new("Konsultantske usluge", "sat"),
        new("Dostava", "kom"),
        new("Čišćenje prostora", "m2"),
        new("Iznajmljivanje viljuškara", "sat"),
        new("Obuka zaposlenih", "sat"),
        new("Instalacija softvera", "kom"),
        new("Knjigovodstvene usluge", "sat"),
        new("Elektroinstalaterski radovi", "sat"),
        new("Moleraj", "m2"),
        new("Utovar i istovar", "t"),
    ];
}

using MenoScreen.Models;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Data;

// Loads draft translations for the questions of version 1.
// DRAFT WORDING: must be reviewed by fluent speakers before clinical use.
public static class TranslationSeed
{
    private static readonly Dictionary<string, Dictionary<string, string>> Blocks = new()
    {
        ["af"] = new()
        {
            ["Patient Context"] = "Pasiëntkonteks",
            ["Cognitive & Mood"] = "Kognisie en bui",
            ["Vasomotor & Sleep"] = "Warmtegolwe en slaap",
            ["Musculoskeletal & Energy"] = "Spiere, gewrigte en energie",
            ["Urogenital & Sexual Health"] = "Urogenitale en seksuele gesondheid",
            ["Quality of Life Impact"] = "Impak op lewensgehalte",
            ["Safety Flag"] = "Veiligheidsvlag"
        },
        ["zu"] = new()
        {
            ["Patient Context"] = "Umongo wesiguli",
            ["Cognitive & Mood"] = "Ingqondo nemizwa",
            ["Vasomotor & Sleep"] = "Ukushisa nokulala",
            ["Musculoskeletal & Energy"] = "Imisipha, amalunga namandla",
            ["Urogenital & Sexual Health"] = "Impilo yesithakazelo nocansi",
            ["Quality of Life Impact"] = "Umthelela empilweni yansuku zonke",
            ["Safety Flag"] = "Uphawu lokuphepha"
        },
        ["xh"] = new()
        {
            ["Patient Context"] = "Imeko yesigulana",
            ["Cognitive & Mood"] = "Ingqondo neemvakalelo",
            ["Vasomotor & Sleep"] = "Ukushushu nokulala",
            ["Musculoskeletal & Energy"] = "Imisipha, amalungu namandla",
            ["Urogenital & Sexual Health"] = "Impilo yamalungu angasese nesondo",
            ["Quality of Life Impact"] = "Impembelelo kumgangatho wobomi",
            ["Safety Flag"] = "Uphawu lokhuseleko"
        }
    };

    // Number -> (text, options). Options only for the context questions that have them.
    // The options must have the same number of choices, in the same order, as the English ones.
    private static readonly Dictionary<string, Dictionary<int, (string Text, string? Options)>> Texts = new()
    {
        ["af"] = new()
        {
            [1] = ("Hoe oud is jy?", null),
            [2] = ("Wanneer het jou menstruasie laas verander of gestop?",
                   "Nog gereeld|Word onreëlmatig|Gestop <2 jaar gelede|Gestop >2 jaar gelede|Histerektomie|Nie seker nie"),
            [3] = ("Het 'n dokter al ooit met jou oor menopouse of jou hormone gepraat?", "Ja|Nee|Nie seker nie"),
            [4] = ("Neem jy tans enige gereelde medikasie?", "Ja|Nee|Nie seker nie"),
            [5] = ("Het jy gesukkel om te konsentreer, of gevoel dat jou kop in 'n mis is?", null),
            [6] = ("Het jy meer as gewoonlik name, woorde of waar jy goed gesit het, vergeet?", null),
            [7] = ("Het jy angstig, neerslagtig of huilerig gevoel sonder 'n duidelike rede?", null),
            [8] = ("Het jy meer prikkelbaar as gewoonlik gevoel, of dit moeiliker gevind om jou buie te hanteer?", null),
            [9] = ("Het jy skielike warmtegolwe deur jou liggaam ervaar?", null),
            [10] = ("Het jy natgesweet wakker geword of snags oorverhit gevoel?", null),
            [11] = ("Het jy gesukkel om aan die slaap te raak, of gedurende die nag wakker geword?", null),
            [12] = ("Het jy opgemerk dat jou hart jaag of onreëlmatig klop?", null),
            [13] = ("Het jy gewrigspyn of -styfheid ervaar, veral soggens?", null),
            [14] = ("Voel jy uitgeput selfs na 'n volle nag se slaap?", null),
            [15] = ("Kry jy nuwe hoofpyne, of word jou bestaande hoofpyne meer gereeld of erger?", null),
            [16] = ("Het jy enige vaginale seerheid, irritasie of ongemak ervaar, ook tydens seks?", null),
            [17] = ("Moes jy dringend badkamer toe jaag, of het jy enige lekkasie ervaar?", null),
            [18] = ("Kry jy herhaaldelik urienweginfeksies?", null),
            [19] = ("Het jou belangstelling in seks afgeneem of verdwyn?", null),
            [20] = ("Het hierdie simptome jou vermoë om te werk beïnvloed: jou konsentrasie, produktiwiteit of selfvertroue?", null),
            [21] = ("Het hierdie simptome jou verhoudings met jou lewensmaat, gesin of vriende beïnvloed?", null),
            [22] = ("Oor die algemeen, hoeveel kom hierdie simptome in die pad van jou daaglikse lewe?", null),
            [23] = ("Het jy enige gesondheidstoestande, soos diabetes, hartsiekte, 'n geskiedenis van kanker of 'n outo-immuun toestand, wat ons in ag moet neem voordat ons behandeling bespreek?", null)
        },
        ["zu"] = new()
        {
            [1] = ("Uneminyaka emingaki?", null),
            [2] = ("Ukuya kwakho esikhathini kuguquke noma kwaphela nini okokugcina?",
                   "Isajwayelekile|Iyaguquka|Yaphela <iminyaka emi-2 edlule|Yaphela >iminyaka emi-2 edlule|Ukukhishwa kwesibeletho|Angiqiniseki"),
            [3] = ("Ingabe udokotela wake wakhuluma nawe ngokuphela kokuya esikhathini noma ngamahomoni akho?", "Yebo|Cha|Angiqiniseki"),
            [4] = ("Ingabe okwamanje uthatha imithi njalo?", "Yebo|Cha|Angiqiniseki"),
            [5] = ("Ingabe ubunenkinga yokugxila, noma uzizwa sengathi ingqondo yakho inenkungu?", null),
            [6] = ("Ingabe uye wazizwa ukhohlwa amagama, amazwi, noma lapho ubeke khona izinto ngaphezu kokujwayelekile?", null),
            [7] = ("Ingabe uye wazizwa unexhala, udangele, noma ufuna ukukhala ngaphandle kwesizathu esicacile?", null),
            [8] = ("Ingabe uye wazizwa ucasuka kalula kunokujwayelekile, noma uthola kunzima ukulawula isimo sakho sengqondo?", null),
            [9] = ("Ingabe uye wezwa amagagasi okushisa ngokuzumayo emzimbeni wakho?", null),
            [10] = ("Ingabe uye wavuka usumanzi izithukuthuku, noma uzizwa ushise kakhulu ebusuku?", null),
            [11] = ("Ingabe uye wanenkinga yokulala, noma wavuka phakathi kwabusuku?", null),
            [12] = ("Ingabe uye wabona inhliziyo yakho ishaya ngokushesha noma ngokungajwayelekile?", null),
            [13] = ("Ingabe uye wezwa ubuhlungu noma ukuqina kwamalunga, ikakhulukazi ekuseni?", null),
            [14] = ("Ingabe uzizwa ukhathele kakhulu ngisho nangemva kokulala ubusuku bonke?", null),
            [15] = ("Ingabe uye waba nekhanda elisha elibuhlungu, noma ubuhlungu bekhanda onabo buya ngokuba njalo noma busiba nzima?", null),
            [16] = ("Ingabe uye wezwa ubuhlungu, ukucasuka noma ukungakhululeki esithweni sangasese sowesifazane, kuhlanganise nasekuthintaneni ngokocansi?", null),
            [17] = ("Ingabe uye waphoqeleka ukushesha ukuya endlini encane, noma wakhipha umchamo ngokungazibambi?", null),
            [18] = ("Ingabe uye wathola ukutheleleka kwendlela yomchamo kaningi?", null),
            [19] = ("Ingabe isifiso sakho socansi sehlile noma sanyamalala?", null),
            [20] = ("Ingabe lezi zimpawu ziye zaphazamisa umsebenzi wakho: ukugxila, ukukhiqiza, noma ukuzethemba?", null),
            [21] = ("Ingabe lezi zimpawu ziye zaphazamisa ubuhlobo bakho nomlingani wakho, umndeni, noma abangane?", null),
            [22] = ("Sekukonke, lezi zimpawu ziphazamisa kangakanani impilo yakho yansuku zonke?", null),
            [23] = ("Ingabe unazo izimo zempilo, ezifana nesifo sikashukela, isifo senhliziyo, umlando womdlavuza, noma isimo se-autoimmune, okufanele sicabangele ngaphambi kokuxoxa ngokwelashwa?", null)
        },
        ["xh"] = new()
        {
            [1] = ("Uneminyaka emingaphi?", null),
            [2] = ("Ukuya kwakho exesheni kutshintshe okanye kwaphela nini okokugqibela?",
                   "Iqhelekile|Iyatshintsha|Yaphela <kwiminyaka eyi-2 edlulileyo|Yaphela >kwiminyaka eyi-2 edlulileyo|Ukususwa kwesizalo|Andiqinisekanga"),
            [3] = ("Ingaba ugqirha wakha wathetha nawe ngokuphela kokuya exesheni okanye ngee-hormone zakho?", "Ewe|Hayi|Andiqinisekanga"),
            [4] = ("Ingaba okwangoku uthatha amayeza rhoqo?", "Ewe|Hayi|Andiqinisekanga"),
            [5] = ("Ingaba ubunengxaki yokugxila, okanye uziva ngathi ingqondo yakho inenkungu?", null),
            [6] = ("Ingaba ubuthanda ukulibala amagama, amazwi, okanye apho ubeke khona izinto ngaphezu kwesiqhelo?", null),
            [7] = ("Ingaba ubuziva unexhala, udandathekile, okanye ufuna ukulila ngaphandle kwesizathu esicacileyo?", null),
            [8] = ("Ingaba ubuziva ucaphuka lula kunesiqhelo, okanye uyifumana kunzima ukulawula iimvakalelo zakho?", null),
            [9] = ("Ingaba ubuva amaza obushushu ngesiquphe emzimbeni wakho?", null),
            [10] = ("Ingaba ubuvuka unxitywe yimidiza, okanye uziva ushushu kakhulu ebusuku?", null),
            [11] = ("Ingaba ubunengxaki yokulala, okanye uvuka phakathi ebusuku?", null),
            [12] = ("Ingaba ubuqaphele intliziyo yakho ibaleka, okanye ikhaba ngokungaqhelekanga?", null),
            [13] = ("Ingaba ubuva intlungu okanye ukuqina kwamalungu, ngakumbi ekuseni?", null),
            [14] = ("Ingaba uziva udiniwe nasemva kokulala ubusuku bonke?", null),
            [15] = ("Ingaba ububa nentloko ebuhlungu entsha, okanye intloko ebuhlungu oyaziyo iya isiba rhoqo okanye iya isiba nzima?", null),
            [16] = ("Ingaba ubuva intlungu, ukucaphuka okanye ukungakhululeki kwindawo yangasese yabasetyhini, kuquka ngexesha lesondo?", null),
            [17] = ("Ingaba ubuphoqeleka ukukhawuleza uye endlwini yangasese, okanye ube nokuvuza komchamo?", null),
            [18] = ("Ingaba ububa neentsholongwane zendlela yomchamo kaninzi?", null),
            [19] = ("Ingaba umdla wakho wesondo wehlile okanye waphela?", null),
            [20] = ("Ingaba ezi mpawu zichaphazele ukukwazi kwakho ukusebenza: ukugxila, imveliso, okanye ukuzithemba?", null),
            [21] = ("Ingaba ezi mpawu zichaphazele ubudlelwane bakho nesithandwa sakho, usapho, okanye abahlobo?", null),
            [22] = ("Ngokubanzi, zithintela kangakanani ezi mpawu ubomi bakho bemihla ngemihla?", null),
            [23] = ("Ingaba unazo naziphi na iimeko zempilo, ezifana nesifo seswekile, isifo sentliziyo, imbali yekhansa, okanye imeko ye-autoimmune, ekufuneka siyicinge phambi kokuxoxa ngonyango?", null)
        }
    };

    // Safe to run on every start-up: it only adds what is missing.
    // Only version 1 is seeded. Later versions are made with "Make a copy",
    // which copies the translations along with the questions.
    public static async Task SeedTranslationsAsync(ApplicationDbContext db)
    {
        var questions = await db.Questions
            .Include(q => q.QuestionSetVersion)
            .Include(q => q.Translations)
            .Where(q => q.QuestionSetVersion.VersionNumber == 1)
            .ToListAsync();

        foreach (var q in questions)
        {
            foreach (var (lang, byNumber) in Texts)
            {
                if (q.Translations.Any(t => t.Language == lang)) continue;
                if (!byNumber.TryGetValue(q.Number, out var entry)) continue;

                Blocks[lang].TryGetValue(q.Block, out var block);

                q.Translations.Add(new QuestionTranslation
                {
                    Language = lang,
                    Block = block ?? string.Empty,
                    Text = entry.Text,
                    Options = entry.Options
                });
            }
        }

        await db.SaveChangesAsync();
    }
}
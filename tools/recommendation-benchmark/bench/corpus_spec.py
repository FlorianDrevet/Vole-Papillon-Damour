"""Corpus annoté du benchmark : la vérité de référence.

Chaque œuvre porte des étiquettes posées à la main. Les MÉTHODES n'y ont jamais accès :
elles ne voient que ce que la BnF et Open Library renvoient. Seule l'ÉVALUATION lit ces
étiquettes pour noter les voisins proposés (voir metrics.grade).

Champs :
- key       : identifiant de l'œuvre (les éditions d'une même œuvre partagent la clé)
- title     : titre cherché à la BnF (zone 200$a, comparé sans accents ni casse)
- author    : nom d'auteur cherché à la BnF
- audience  : jeunesse (≤ 12 ans) | ado | adulte | tout-public
- form      : roman | conte | bd | manga | essai | pratique
- clusters  : familles thématiques ; partager une famille rend deux livres « proches »
- series    : (identifiant de série, numéro de tome) ou None
- editions  : nombre d'éditions distinctes à prendre (2 = tester la fuite « même œuvre »)
- tome      : numéro exigé dans la notice (461$v ou 200$h) quand le titre est commun
- adaptation_of : clé de l'œuvre adaptée (une BD tirée d'un roman)

Ces étiquettes sont un jugement éditorial : elles sont modifiables, et le rapport
indique leurs limites.
"""

from dataclasses import dataclass, field


@dataclass(frozen=True)
class Work:
    key: str
    title: str
    author: str
    audience: str
    form: str
    clusters: tuple[str, ...]
    series: tuple[str, int] | None = None
    editions: int = 1
    tome: int | None = None
    adaptation_of: str | None = None


def W(key, title, author, audience, form, clusters, series=None, editions=1, tome=None, adaptation_of=None):
    return Work(key, title, author, audience, form, tuple(clusters), series, editions, tome, adaptation_of)


WORKS: list[Work] = [
    # --- Fantasy jeunesse et école de magie -------------------------------------------
    W("hp1", "Harry Potter à l'école des sorciers", "Rowling", "jeunesse", "roman", ["ecole-de-magie", "fantasy-jeunesse"], ("harry-potter", 1), editions=2),
    W("hp2", "Harry Potter et la chambre des secrets", "Rowling", "jeunesse", "roman", ["ecole-de-magie", "fantasy-jeunesse"], ("harry-potter", 2)),
    W("hp3", "Harry Potter et le prisonnier d'Azkaban", "Rowling", "jeunesse", "roman", ["ecole-de-magie", "fantasy-jeunesse"], ("harry-potter", 3)),
    W("percy1", "Le voleur de foudre", "Riordan", "jeunesse", "roman", ["fantasy-jeunesse", "mythologie"], ("percy-jackson", 1)),
    W("ewilan1", "D'un monde à l'autre", "Bottero", "jeunesse", "roman", ["fantasy-jeunesse"], ("ewilan", 1)),
    W("narnia", "Le lion, la sorcière blanche et l'armoire magique", "Lewis", "jeunesse", "roman", ["fantasy-jeunesse"]),
    W("tara1", "Les sortceliers", "Audouin-Mamikonian", "jeunesse", "roman", ["ecole-de-magie", "fantasy-jeunesse"], ("tara-duncan", 1)),
    W("tobie1", "La vie suspendue", "Fombelle", "jeunesse", "roman", ["fantasy-jeunesse"], ("tobie-lolness", 1)),
    W("passemiroir1", "Les fiancés de l'hiver", "Dabos", "ado", "roman", ["fantasy-jeunesse"], ("passe-miroir", 1)),
    W("pullman1", "Les royaumes du Nord", "Pullman", "ado", "roman", ["fantasy-jeunesse"], ("croisee-des-mondes", 1)),
    W("eragon", "Eragon", "Paolini", "ado", "roman", ["fantasy-jeunesse"]),
    W("twilight1", "Fascination", "Meyer", "ado", "roman", ["fantasy-jeunesse", "romance"], ("twilight", 1)),
    W("hobbit", "Bilbo le hobbit", "Tolkien", "tout-public", "roman", ["fantasy-jeunesse", "fantasy-adulte"]),
    # --- Fantasy adulte ---------------------------------------------------------------
    W("sda1", "La communauté de l'anneau", "Tolkien", "adulte", "roman", ["fantasy-adulte"], ("seigneur-des-anneaux", 1)),
    W("tronedefer1", "Le trône de fer", "Martin", "adulte", "roman", ["fantasy-adulte"], ("trone-de-fer", 1)),
    W("assassin1", "L'apprenti assassin", "Hobb", "adulte", "roman", ["fantasy-adulte"], ("assassin-royal", 1)),
    W("sorceleur1", "Le dernier vœu", "Sapkowski", "adulte", "roman", ["fantasy-adulte"], ("sorceleur", 1)),
    W("horde", "La horde du contrevent", "Damasio", "adulte", "roman", ["fantasy-adulte", "sf"]),
    # --- Dystopies --------------------------------------------------------------------
    W("1984", "1984", "Orwell", "adulte", "roman", ["dystopie"]),
    W("1984bd", "1984", "Coste", "adulte", "bd", ["dystopie"], adaptation_of="1984"),
    W("meilleurmonde", "Le meilleur des mondes", "Huxley", "adulte", "roman", ["dystopie", "sf"]),
    W("fahrenheit", "Fahrenheit 451", "Bradbury", "adulte", "roman", ["dystopie", "sf"]),
    W("servante", "La servante écarlate", "Atwood", "adulte", "roman", ["dystopie", "feminisme"]),
    W("ravage", "Ravage", "Barjavel", "adulte", "roman", ["dystopie", "sf"]),
    W("hunger1", "Hunger games", "Collins", "ado", "roman", ["dystopie"], ("hunger-games", 1)),
    W("divergente1", "Divergente", "Roth", "ado", "roman", ["dystopie"], ("divergente", 1)),
    W("labyrinthe1", "Le labyrinthe", "Dashner", "ado", "roman", ["dystopie"], ("l-epreuve", 1)),
    W("passeur", "Le passeur", "Lowry", "ado", "roman", ["dystopie"]),
    # --- Science-fiction --------------------------------------------------------------
    W("fondation1", "Fondation", "Asimov", "adulte", "roman", ["sf"], ("fondation", 1)),
    W("fondation2", "Fondation et empire", "Asimov", "adulte", "roman", ["sf"], ("fondation", 2)),
    W("dune", "Dune", "Herbert", "adulte", "roman", ["sf"], editions=2),
    W("hyperion", "Hypérion", "Simmons", "adulte", "roman", ["sf"]),
    W("robots", "Les robots", "Asimov", "adulte", "roman", ["sf"]),
    W("nuittemps", "La nuit des temps", "Barjavel", "adulte", "roman", ["sf", "romance"]),
    W("martiennes", "Chroniques martiennes", "Bradbury", "adulte", "roman", ["sf"]),
    W("troiscorps", "Le problème à trois corps", "Liu", "adulte", "roman", ["sf"]),
    # --- Enquêtes classiques ----------------------------------------------------------
    W("dixpetits", "Dix petits nègres", "Christie", "adulte", "roman", ["enquete-classique"]),
    W("orient", "Le crime de l'Orient-Express", "Christie", "adulte", "roman", ["enquete-classique"]),
    W("baskerville", "Le chien des Baskerville", "Doyle", "adulte", "roman", ["enquete-classique"]),
    W("etuderouge", "Une étude en rouge", "Doyle", "adulte", "roman", ["enquete-classique"]),
    W("chienjaune", "Le chien jaune", "Simenon", "adulte", "roman", ["enquete-classique"]),
    W("maigretpiege", "Maigret tend un piège", "Simenon", "adulte", "roman", ["enquete-classique"]),
    W("lupin", "Arsène Lupin, gentleman-cambrioleur", "Leblanc", "tout-public", "roman", ["enquete-classique", "aventure-classique"]),
    W("nomrose", "Le nom de la rose", "Eco", "adulte", "roman", ["enquete-classique"]),
    # --- Thrillers et polars ----------------------------------------------------------
    W("millenium1", "Les hommes qui n'aimaient pas les femmes", "Larsson", "adulte", "roman", ["polar-nordique", "thriller"], ("millenium", 1), editions=2),
    W("millenium2", "La fille qui rêvait d'un bidon d'essence et d'une allumette", "Larsson", "adulte", "roman", ["polar-nordique", "thriller"], ("millenium", 2)),
    W("bonhomme", "Le bonhomme de neige", "Nesbø", "adulte", "roman", ["polar-nordique", "thriller"]),
    W("misericorde", "Miséricorde", "Adler-Olsen", "adulte", "roman", ["polar-nordique", "thriller"]),
    W("femmevert", "La femme en vert", "Indridason", "adulte", "roman", ["polar-nordique", "thriller"]),
    W("rivieres", "Les rivières pourpres", "Grangé", "adulte", "roman", ["thriller"]),
    W("chuchoteur", "Le chuchoteur", "Carrisi", "adulte", "roman", ["thriller"]),
    W("davinci", "Da Vinci code", "Brown", "adulte", "roman", ["thriller"]),
    W("quebert", "La vérité sur l'affaire Harry Quebert", "Dicker", "adulte", "roman", ["thriller"]),
    W("agneaux", "Le silence des agneaux", "Harris", "adulte", "roman", ["thriller"]),
    # --- Aventure classique -----------------------------------------------------------
    W("montecristo", "Le comte de Monte-Cristo", "Dumas", "adulte", "roman", ["aventure-classique"]),
    W("mousquetaires", "Les trois mousquetaires", "Dumas", "adulte", "roman", ["aventure-classique"]),
    W("iletresor", "L'île au trésor", "Stevenson", "tout-public", "roman", ["aventure-classique"]),
    W("vingtmille", "Vingt mille lieues sous les mers", "Verne", "tout-public", "roman", ["aventure-classique", "sf"]),
    W("tourdumonde", "Le tour du monde en quatre-vingts jours", "Verne", "tout-public", "roman", ["aventure-classique"]),
    W("crocblanc", "Croc-Blanc", "London", "tout-public", "roman", ["aventure-classique", "animaux"]),
    W("appelforet", "L'appel de la forêt", "London", "tout-public", "roman", ["aventure-classique", "animaux"]),
    # --- Réalisme du XIXe siècle ------------------------------------------------------
    W("germinal", "Germinal", "Zola", "adulte", "roman", ["realisme-xixe"], editions=2),
    W("assommoir", "L'assommoir", "Zola", "adulte", "roman", ["realisme-xixe"]),
    W("bovary", "Madame Bovary", "Flaubert", "adulte", "roman", ["realisme-xixe"]),
    W("belami", "Bel-Ami", "Maupassant", "adulte", "roman", ["realisme-xixe"]),
    W("unevie", "Une vie", "Maupassant", "adulte", "roman", ["realisme-xixe"]),
    W("goriot", "Le père Goriot", "Balzac", "adulte", "roman", ["realisme-xixe"]),
    W("miserables", "Les misérables", "Hugo", "adulte", "roman", ["realisme-xixe"]),
    W("rougenoir", "Le rouge et le noir", "Stendhal", "adulte", "roman", ["realisme-xixe"]),
    # --- Absurde, existentialisme -----------------------------------------------------
    W("etranger", "L'étranger", "Camus", "adulte", "roman", ["absurde"], editions=2),
    W("peste", "La peste", "Camus", "adulte", "roman", ["absurde"]),
    W("nausee", "La nausée", "Sartre", "adulte", "roman", ["absurde"]),
    W("proces", "Le procès", "Kafka", "adulte", "roman", ["absurde"]),
    W("metamorphose", "La métamorphose", "Kafka", "adulte", "roman", ["absurde"]),
    # --- Contes philosophiques et quêtes initiatiques ---------------------------------
    W("petitprince", "Le petit prince", "Saint-Exupéry", "tout-public", "conte", ["quete-initiatique", "conte-philosophique"], editions=2),
    W("candide", "Candide", "Voltaire", "adulte", "conte", ["conte-philosophique"]),
    W("zadig", "Zadig", "Voltaire", "adulte", "conte", ["conte-philosophique"]),
    W("alchimiste", "L'alchimiste", "Coelho", "adulte", "roman", ["quete-initiatique"]),
    W("siddhartha", "Siddhartha", "Hesse", "adulte", "roman", ["quete-initiatique"]),
    W("goeland", "Jonathan Livingston le goéland", "Bach", "tout-public", "conte", ["quete-initiatique"]),
    W("prophete", "Le prophète", "Gibran", "adulte", "conte", ["quete-initiatique"]),
    # --- Guerre de 14-18 et XXe siècle ------------------------------------------------
    W("aurevoir", "Au revoir là-haut", "Lemaitre", "adulte", "roman", ["guerre-14-18"], ("enfants-du-desastre", 1)),
    W("couleurs", "Couleurs de l'incendie", "Lemaitre", "adulte", "roman", ["roman-historique-xxe"], ("enfants-du-desastre", 2)),
    W("aurevoirbd", "Au revoir là-haut", "De Metter", "adulte", "bd", ["guerre-14-18"], adaptation_of="aurevoir"),
    W("croixbois", "Les croix de bois", "Dorgelès", "adulte", "roman", ["guerre-14-18"]),
    W("alouest", "À l'ouest rien de nouveau", "Remarque", "adulte", "roman", ["guerre-14-18"]),
    W("chambreofficiers", "La chambre des officiers", "Dugain", "adulte", "roman", ["guerre-14-18"]),
    W("longdimanche", "Un long dimanche de fiançailles", "Japrisot", "adulte", "roman", ["guerre-14-18", "roman-historique-xxe"]),
    W("grandtroupeau", "Le grand troupeau", "Giono", "adulte", "roman", ["guerre-14-18"]),
    # --- Feel-good et contemporain ----------------------------------------------------
    W("herisson", "L'élégance du hérisson", "Barbery", "adulte", "roman", ["feel-good"]),
    W("tresse", "La tresse", "Colombani", "adulte", "roman", ["feel-good"]),
    W("listeenvies", "La liste de mes envies", "Delacourt", "adulte", "roman", ["feel-good"]),
    W("ensemble", "Ensemble, c'est tout", "Gavalda", "adulte", "roman", ["feel-good"]),
    W("liseur", "Le liseur du 6h27", "Didierlaurent", "adulte", "roman", ["feel-good"]),
    W("deuxiemevie", "Ta deuxième vie commence quand tu comprends que tu n'en as qu'une", "Giordano", "adulte", "roman", ["feel-good", "developpement-personnel"]),
    W("gratitudes", "Les gratitudes", "Vigan", "adulte", "roman", ["feel-good"]),
    W("etapres", "Et après", "Musso", "adulte", "roman", ["feel-good", "romance"]),
    W("avanttoi", "Avant toi", "Moyes", "adulte", "roman", ["feel-good", "romance"]),
    # --- Romance ----------------------------------------------------------------------
    W("orgueil", "Orgueil et préjugés", "Austen", "adulte", "roman", ["romance", "romance-classique"]),
    W("janeeyre", "Jane Eyre", "Brontë", "adulte", "roman", ["romance", "romance-classique"]),
    W("hurlevent", "Les hauts de Hurlevent", "Brontë", "adulte", "roman", ["romance", "romance-classique"]),
    W("etoiles", "Nos étoiles contraires", "Green", "ado", "roman", ["romance"]),
    # --- Humour et enfance ------------------------------------------------------------
    W("nicolas", "Le petit Nicolas", "Goscinny", "jeunesse", "roman", ["humour-enfance"]),
    W("jdd1", "Journal d'un dégonflé", "Kinney", "jeunesse", "roman", ["humour-enfance"], ("journal-degonfle", 1), tome=1),
    W("jdd2", "Journal d'un dégonflé", "Kinney", "jeunesse", "roman", ["humour-enfance"], ("journal-degonfle", 2), tome=2),
    W("charlie", "Charlie et la chocolaterie", "Dahl", "jeunesse", "roman", ["humour-enfance"]),
    W("matilda", "Matilda", "Dahl", "jeunesse", "roman", ["humour-enfance"]),
    W("bgg", "Le bon gros géant", "Dahl", "jeunesse", "roman", ["humour-enfance", "fantasy-jeunesse"]),
    W("sophie", "Les malheurs de Sophie", "Ségur", "jeunesse", "roman", ["humour-enfance"]),
    W("fifi", "Fifi Brindacier", "Lindgren", "jeunesse", "roman", ["humour-enfance"]),
    # --- Animaux ----------------------------------------------------------------------
    W("clans1", "Retour à l'état sauvage", "Hunter", "jeunesse", "roman", ["animaux", "fantasy-jeunesse"], ("guerre-des-clans", 1)),
    W("clans2", "À feu et à sang", "Hunter", "jeunesse", "roman", ["animaux", "fantasy-jeunesse"], ("guerre-des-clans", 2)),
    W("belleseb", "Belle et Sébastien", "Aubry", "jeunesse", "roman", ["animaux"]),
    # --- BD : humour et gag -----------------------------------------------------------
    W("gaston", "Gala de gaffes à gogo", "Franquin", "tout-public", "bd", ["bd-gag"]),
    W("titeuf1", "Dieu, le sexe et les bretelles", "Zep", "jeunesse", "bd", ["bd-gag"], ("titeuf", 1)),
    W("petitspirou", "Dis bonjour à la dame !", "Tome", "jeunesse", "bd", ["bd-gag"], ("petit-spirou", 1)),
    W("kidpaddle", "Jeux de vilains", "Midam", "jeunesse", "bd", ["bd-gag"], ("kid-paddle", 1)),
    W("cedric", "Premières classes", "Cauvin", "jeunesse", "bd", ["bd-gag"], ("cedric", 1)),
    W("adele1", "Tout ça finira mal", "Tan", "jeunesse", "bd", ["bd-gag", "humour-enfance"], ("mortelle-adele", 1)),
    # --- BD : aventure franco-belge ---------------------------------------------------
    W("asterix1", "Astérix le Gaulois", "Goscinny", "tout-public", "bd", ["bd-aventure"], ("asterix", 1)),
    W("asterix2", "La serpe d'or", "Goscinny", "tout-public", "bd", ["bd-aventure"], ("asterix", 2)),
    W("tintintibet", "Tintin au Tibet", "Hergé", "tout-public", "bd", ["bd-aventure"], ("tintin", 20)),
    W("tintinlune", "Objectif Lune", "Hergé", "tout-public", "bd", ["bd-aventure"], ("tintin", 16)),
    W("luckyluke", "Dalton City", "Morris", "tout-public", "bd", ["bd-aventure"]),
    W("zorglub", "Z comme Zorglub", "Franquin", "tout-public", "bd", ["bd-aventure"]),
    W("espadon", "Le secret de l'Espadon", "Jacobs", "tout-public", "bd", ["bd-aventure"]),
    W("thorgal", "La magicienne trahie", "Van Hamme", "tout-public", "bd", ["bd-aventure", "fantasy-adulte"], ("thorgal", 1)),
    # --- BD : polar et roman graphique adulte -----------------------------------------
    W("blacksad", "Quelque part entre les ombres", "Díaz Canales", "adulte", "bd", ["bd-polar", "thriller"], ("blacksad", 1)),
    W("largo1", "L'héritier", "Van Hamme", "adulte", "bd", ["bd-polar", "thriller"], ("largo-winch", 1)),
    W("xiii1", "Le jour du soleil noir", "Van Hamme", "adulte", "bd", ["bd-polar", "thriller"], ("xiii", 1)),
    W("maus", "Maus", "Spiegelman", "adulte", "bd", ["bd-historique"]),
    W("persepolis", "Persepolis", "Satrapi", "adulte", "bd", ["bd-historique"]),
    W("birmanes", "Chroniques birmanes", "Delisle", "adulte", "bd", ["bd-historique"]),
    # --- Manga shōnen -----------------------------------------------------------------
    W("onepiece1", "One piece", "Oda", "ado", "manga", ["shonen"], ("one-piece", 1), tome=1),
    W("onepiece2", "One piece", "Oda", "ado", "manga", ["shonen"], ("one-piece", 2), tome=2),
    W("naruto1", "Naruto", "Kishimoto", "ado", "manga", ["shonen"], ("naruto", 1), tome=1),
    W("dragonball1", "Dragon ball", "Toriyama", "ado", "manga", ["shonen"], ("dragon-ball", 1), tome=1),
    W("titans1", "L'attaque des titans", "Isayama", "ado", "manga", ["shonen", "dystopie"], ("attaque-des-titans", 1), tome=1),
    W("mha1", "My hero academia", "Horikoshi", "ado", "manga", ["shonen"], ("my-hero-academia", 1), tome=1),
    W("deathnote1", "Death note", "Ohba", "ado", "manga", ["shonen", "thriller"], ("death-note", 1), tome=1),
    # --- Essais : histoire, sciences, idées -------------------------------------------
    W("sapiens", "Sapiens", "Harari", "adulte", "essai", ["histoire-humanite"], editions=2),
    W("homodeus", "Homo deus", "Harari", "adulte", "essai", ["histoire-humanite"]),
    W("diamond", "De l'inégalité parmi les sociétés", "Diamond", "adulte", "essai", ["histoire-humanite"]),
    W("hawking", "Une brève histoire du temps", "Hawking", "adulte", "essai", ["vulgarisation-scientifique"]),
    W("genegoiste", "Le gène égoïste", "Dawkins", "adulte", "essai", ["vulgarisation-scientifique"]),
    W("cosmos", "Cosmos", "Sagan", "adulte", "essai", ["vulgarisation-scientifique"]),
    # --- Essais : féminisme -----------------------------------------------------------
    W("deuxiemesexe", "Le deuxième sexe", "Beauvoir", "adulte", "essai", ["feminisme"]),
    W("kingkong", "King Kong théorie", "Despentes", "adulte", "essai", ["feminisme"]),
    W("chambresoi", "Une chambre à soi", "Woolf", "adulte", "essai", ["feminisme"]),
    W("sorcieres", "Sorcières", "Chollet", "adulte", "essai", ["feminisme"]),
    # --- Pratique : cuisine -----------------------------------------------------------
    W("jesaiscuisiner", "Je sais cuisiner", "Mathiot", "adulte", "pratique", ["cuisine"]),
    W("patisserie", "Pâtisserie !", "Felder", "adulte", "pratique", ["cuisine"]),
    W("jerusalem", "Jérusalem", "Ottolenghi", "adulte", "pratique", ["cuisine"]),
    W("cuisinereference", "La cuisine de référence", "Maincent-Morel", "adulte", "pratique", ["cuisine"]),
    # --- Développement personnel ------------------------------------------------------
    W("quatreaccords", "Les quatre accords toltèques", "Ruiz", "adulte", "essai", ["developpement-personnel", "quete-initiatique"]),
    W("momentpresent", "Le pouvoir du moment présent", "Tolle", "adulte", "essai", ["developpement-personnel"]),
    W("pereriche", "Père riche, père pauvre", "Kiyosaki", "adulte", "essai", ["developpement-personnel"]),
    W("septhabitudes", "Les 7 habitudes de ceux qui réalisent tout ce qu'ils entreprennent", "Covey", "adulte", "essai", ["developpement-personnel"]),
]

WORKS_BY_KEY = {w.key: w for w in WORKS}
assert len(WORKS_BY_KEY) == len(WORKS), "clé d'œuvre en double"

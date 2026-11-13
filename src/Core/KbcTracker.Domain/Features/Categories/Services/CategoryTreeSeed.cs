namespace KbcTracker.Domain.Features.Categories.Services;

/// <summary>
/// Author      : Gihed Annabi
/// Date        : 11-2026
/// Purpose     : The default category tree, written in code: the 13 COICOP 2018 divisions with friendly second and
///              third levels, plus the roots outside COICOP (income, savings, transfers, loans, taxes, gifts, cash,
///              unsorted direct debits and other). Classified by purpose, so shampoo is Personal care wherever it was
///              bought.
///
///              The keywords are split into a worldwide core (global brands and generic words in French, Dutch,
///              English and German) and a Belgian pack, because the bank sends no merchant category code and the
///              merchant has to be recognised by name. A trailing space in a keyword (<c>"AD "</c>, <c>"CM "</c>,
///              <c>"BAR "</c>) is deliberate: it stops the keyword matching longer words. Children take the colour
///              and type of their root unless they set their own.
/// </summary>
internal static class CategoryTreeSeed
{
	#region Static Instances
	/// <summary>
	/// The nodes in tree order: a parent always comes before its children.
	/// </summary>
	public static readonly IReadOnlyList<CategorySeedNode> Nodes =
	[
		Root("groceries", "Food & non-alcoholic drinks", "01", "#f2994a", ["LIDL", "ALDI", "CARREFOUR", "SPAR", "AUCHAN", "TESCO", "REWE", "EDEKA", "ALBERT HEIJN", "INTERMARCHE", "SUPERMARKET", "SUPERMARCHE"], ["DELHAIZE", "COLRUYT", "AD ", "OKAY", "MATCH", "CORA", "PROXY", "BIO-PLANET"]),
		Child("food", "groceries", "Food", "01.1", [], []),
		Child("food-bread-cereals", "food", "Bread, cereals & bakery", "01.1", ["BOULANGERIE", "BAKKERIJ", "BAKERY", "BACKEREI", "PATISSERIE", "BANKETBAKKERIJ"], []),
		Child("food-meat", "food", "Meat", "01.1", ["BOUCHERIE", "SLAGERIJ", "SLAGER", "BUTCHER", "METZGEREI", "CHARCUTERIE"], []),
		Child("food-fish", "food", "Fish & seafood", "01.1", ["POISSONNERIE", "VISHANDEL", "FISHMONGER"], []),
		Child("food-dairy", "food", "Milk, dairy & eggs", "01.1", ["FROMAGERIE", "KAASHANDEL"], []),
		Child("food-oils", "food", "Oils & fats", "01.1", [], []),
		Child("food-fruit-veg", "food", "Fruit & vegetables", "01.1", ["PRIMEUR", "GROENTEN", "GREENGROCER"], []),
		Child("food-sweets", "food", "Sugar, sweets & desserts", "01.1", ["CHOCOLATIER", "CONFISERIE", "GODIVA"], ["NEUHAUS", "LEONIDAS", "PIERRE MARCOLINI"]),
		Child("food-ready-meals", "food", "Ready meals & other food", "01.1", ["PICARD"], []),
		Child("drinks", "groceries", "Non-alcoholic drinks", "01.2", [], []),
		Child("drinks-coffee-tea", "drinks", "Coffee, tea & cocoa", "01.2", ["NESPRESSO"], []),
		Child("drinks-water-soft", "drinks", "Water, soft drinks & juices", "01.2", [], []),

		Root("alcohol-tobacco", "Alcohol & tobacco", "02", "#8e44ad", [], []),
		Child("alcohol", "alcohol-tobacco", "Alcoholic drinks", "02.1", ["NICOLAS", "CAVISTE", "LIQUOR", "WINE SHOP", "DRANKENCENTRALE"], []),
		Child("tobacco", "alcohol-tobacco", "Tobacco", "02.3", ["TABAC", "TABAK", "TOBACCO"], []),

		Root("clothing", "Clothing & footwear", "03", "#219653", ["H&M", "ZARA", "PRIMARK", "C&A", "UNIQLO", "MANGO", "ZALANDO"], ["JBC", "ZEB", "BEL&BO"]),
		Child("clothing-clothes", "clothing", "Clothing", "03.1", [], []),
		Child("clothing-shoes", "clothing", "Footwear", "03.2", ["CHAUSSURES", "SCHOENEN", "SHOES", "FOOT LOCKER"], ["BRANTANO"]),

		Root("housing", "Housing & energy", "04", "#bb6bd9", [], []),
		Child("housing-rent", "housing", "Rent", "04.1", ["RENT", "LOYER", "HUUR", "MIETE"], []),
		Child("housing-repair", "housing", "Maintenance & repair", "04.3", ["PLOMBIER", "LOODGIETER", "PLUMBER", "ELECTRICIEN"], []),
		Child("housing-charges", "housing", "Water, waste & co-ownership", "04.4", ["SYNDIC"], ["VIVAQUA", "HYDROBRU", "DE WATERGROEP", "FARYS"]),
		Child("housing-energy", "housing", "Electricity, gas & heating", "04.5", ["ENGIE", "TOTALENERGIES", "ESSENT"], ["ELECTRABEL", "LUMINUS", "MEGA", "SIBELGA", "FLUVIUS"]),

		Root("home", "Home & household", "05", "#e67e22", [], []),
		Child("home-furniture", "home", "Furniture & decoration", "05.1", ["IKEA", "JYSK", "MAISONS DU MONDE"], ["CASA"]),
		Child("home-textiles", "home", "Home textiles", "05.2", [], []),
		Child("home-appliances", "home", "Appliances", "05.3", [], []),
		Child("home-kitchenware", "home", "Kitchenware & utensils", "05.4", [], []),
		Child("home-diy-garden", "home", "Tools, DIY & garden", "05.5", ["LEROY MERLIN", "BAUHAUS", "OBI", "HORNBACH", "GAMMA", "PRAXIS"], ["BRICO", "HUBO", "MR BRICOLAGE", "AVEVE"]),
		Child("home-cleaning", "home", "Cleaning & household services", "05.6", [], []),

		Root("health", "Health", "06", "#00cec9", [], []),
		Child("health-pharmacy", "health", "Medicines & pharmacy", "06.1", ["PHARMACIE", "APOTHEEK", "PHARMACY", "APOTHEKE", "FARMACIA"], ["MULTIPHARMA"]),
		Child("health-glasses", "health", "Glasses & medical devices", "06.1", ["PEARLE", "HANS ANDERS", "SPECSAVERS", "OPTICIEN", "OPTIEK", "OPTICIAN"], []),
		Child("health-outpatient", "health", "Doctors & dentists", "06.2", ["DOKTER", "DOCTEUR", "MEDECIN", "HUISARTS", "DENTISTE", "TANDARTS", "DENTIST", "PHYSIO", "KINE"], []),
		Child("health-hospital", "health", "Hospital", "06.3", ["HOPITAL", "HOSPITAL", "ZIEKENHUIS", "KLINIEK", "CLINIQUE"], ["CHU ", "UZ "]),
		Child("health-other", "health", "Other health services", "06.4", ["LABO", "LABORATOIRE"], []),

		Root("transport", "Transport", "07", "#3454d1", [], []),
		Child("transport-vehicles", "transport", "Buying vehicles", "07.1", [], []),
		Child("transport-own", "transport", "Running own vehicles", "07.2", [], []),
		Child("car", "transport-own", "Car", "07.2", [], []),
		Child("car-fuel", "car", "Fuel & charging", "07.2", ["SHELL", "ESSO", "TOTAL", "BP", "Q8", "TEXACO", "GULF", "LUKOIL", "ARAL", "AGIP", "IONITY", "FASTNED", "ALLEGO"], ["DATS 24", "MAES"]),
		Child("car-maintenance", "car", "Maintenance & repair", "07.2", ["GARAGE", "NORAUTO", "CARGLASS", "AUTOMOBILE", "CARWASH", "FITWASH"], ["D'IETEREN", "VROOM", "TOYOTA"]),
		Child("car-parking", "car", "Parking & tolls", "07.2", ["PARKING", "Q-PARK", "INDIGO", "INTERPARKING", "PEAGE", "VIGNETTE"], ["4411", "SHOPANDGO"]),
		Child("car-inspection", "car", "Inspection & tax", "07.2", [], ["AUTOVEILIGHEID", "KEURING", "CONTROLE TECHNIQUE", "AUTOSECURITE"]),
		Child("motorcycle", "transport-own", "Motorcycle", "07.2", [], []),
		Child("motorcycle-fuel", "motorcycle", "Fuel", "07.2", [], []),
		Child("motorcycle-maint", "motorcycle", "Maintenance & repair", "07.2", [], []),
		Child("bicycle", "transport-own", "Bicycle & e-scooter", "07.2", [], []),
		Child("transport-public", "transport", "Public & paid transport", "07.3", [], ["STIB", "MIVB", "DE LIJN", "TEC"]),
		Child("transport-train", "transport-public", "Train", "07.3", ["SNCF", "EUROSTAR", "DEUTSCHE BAHN", "OUIGO", "TRENITALIA", "RENFE"], ["SNCB", "NMBS"]),
		Child("transport-tram-metro", "transport-public", "Tram & metro", "07.3", [], []),
		Child("transport-bus", "transport-public", "Bus & coach", "07.3", ["FLIXBUS"], []),
		Child("transport-taxi", "transport-public", "Taxi & ride-hailing", "07.3", ["UBER", "BOLT", "TAXI", "HEETCH", "LYFT", "FREENOW"], []),
		Child("transport-plane", "transport-public", "Plane", "07.3", ["RYANAIR", "EASYJET", "TUI FLY", "TRANSAVIA", "VUELING", "KLM", "LUFTHANSA", "AIR FRANCE", "WIZZ"], ["BRUSSELS AIRLINES"]),
		Child("transport-boat", "transport-public", "Boat & ferry", "07.3", ["FERRY", "DFDS"], []),
		Child("transport-pass", "transport-public", "Combined transport passes", "07.3", [], []),
		Child("transport-shared", "transport-public", "Shared bikes, scooters & cars", "07.3", ["LIME", "DOTT", "TIER", "VOI "], ["VILLO", "CAMBIO", "POPPY", "ZITY", "BLUE-BIKE"]),
		Child("transport-goods", "transport", "Delivery & removals", "07.4", ["DEMENAGEMENT", "VERHUIS"], []),

		Root("communication", "Phone & internet", "08", "#16a085", [], []),
		Child("comm-devices", "communication", "Phones, computers & electronics", "08.1", ["MEDIA MARKT", "MEDIAMARKT", "FNAC", "APPLE STORE"], ["COOLBLUE", "VANDEN BORRE", "KREFEL"]),
		Child("comm-software", "communication", "Software & apps", "08.2", ["MICROSOFT", "GITHUB", "OVH", "ADOBE", "GOOGLE"], []),
		Child("comm-services", "communication", "Mobile, internet & TV", "08.3", ["ORANGE", "VODAFONE"], ["PROXIMUS", "TELENET", "BASE", "VOO", "SCARLET", "MOBILE VIKINGS"]),
		Child("comm-postal", "communication", "Postal & parcel services", "08.3", ["DHL", "UPS", "POSTNL"], ["BPOST"]),

		Root("recreation", "Recreation, sport & culture", "09", "#e17055", [], []),
		Child("rec-equipment", "recreation", "Equipment & toys", "09.1", ["LEGO"], ["DREAMLAND"]),
		Child("rec-games", "recreation", "Games & hobbies", "09.2", ["STEAM", "PLAYSTATION", "NINTENDO", "XBOX"], []),
		Child("rec-pets", "recreation", "Pets & garden", "09.3", ["VETERINAIRE", "DIERENARTS", "ZOOPLUS", "MAXI ZOO", "FRESSNAPF"], ["TOM&CO"]),
		Child("rec-sport", "recreation", "Sport & fitness", "09.4", ["BASIC-FIT", "DECATHLON", "FITNESS"], ["JIMS"]),
		Child("rec-streaming", "recreation", "Streaming & subscriptions", "09.6", ["NETFLIX", "SPOTIFY", "DISNEY", "YOUTUBE", "DEEZER", "APPLE.COM", "AMAZON PRIME", "HBO"], ["STREAMZ"]),
		Child("rec-culture", "recreation", "Culture & events", "09.6", ["UGC", "PATHE", "CINEMA", "MUSEE", "MUSEUM", "TICKETMASTER", "THEATRE"], ["KINEPOLIS", "BOZAR", "FLAGEY"]),
		Child("rec-books", "recreation", "Books, newspapers & stationery", "09.7", ["LIBRAIRIE", "BOEKHANDEL", "BOOKSHOP", "RELAY"], ["STANDAARD BOEKHANDEL", "CLUB", "AVA", "FILIGRANES", "PRESS SHOP"]),
		Child("rec-holidays", "recreation", "Package holidays", "09.8", ["TUI", "SUNWEB", "CLUB MED"], []),

		Root("education", "Education", "10", "#6c5ce7", [], []),
		Child("edu-school", "education", "School", "10.1", ["ECOLE", "SCHOOL", "LYCEE", "ATHENEE"], []),
		Child("edu-higher", "education", "Higher education", "10.4", ["UNIVERSITE", "UNIVERSITEIT", "UNIVERSITY", "HOGESCHOOL"], []),
		Child("edu-courses", "education", "Courses & training", "10.5", [], []),

		Root("restaurants-cafes", "Restaurants & accommodation", "11", "#27ae60", [], []),
		Child("eat-restaurants", "restaurants-cafes", "Restaurants", "11.1", ["RESTAURANT", "PIZZA", "PIZZERIA", "SUSHI", "BRASSERIE", "TRATTORIA", "RISTORANTE"], []),
		Child("eat-cafes-bars", "restaurants-cafes", "Cafés & bars", "11.1", ["CAFE", "BAR ", "STARBUCKS", "COFFEE"], []),
		Child("eat-fast-food", "restaurants-cafes", "Fast food & takeaway", "11.1", ["MCDONALD", "BURGER", "KFC", "SUBWAY", "PAUL", "FIVE GUYS"], ["QUICK", "EXKI", "PANOS"]),
		Child("eat-delivery", "restaurants-cafes", "Food delivery", "11.1", ["DELIVEROO", "UBER EATS", "UBER *EATS", "TAKEAWAY", "JUST EAT", "WOLT"], []),
		Child("eat-canteen", "restaurants-cafes", "Canteen", "11.1", ["SODEXO", "CANTINE", "KANTINE"], ["FEDOREST"]),
		Child("accommodation", "restaurants-cafes", "Hotels & stays", "11.2", ["HOTEL", "BOOKING.COM", "AIRBNB", "EXPEDIA", "IBIS", "NOVOTEL", "HILTON"], []),

		Root("finance", "Insurance & financial services", "12", "#0984e3", [], []),
		Child("insurance", "finance", "Insurance", "12.1", ["INSURANCE", "ASSURANCE", "VERZEKERING", "AXA", "ALLIANZ", "GENERALI"], ["ETHIAS", "BALOISE", "P&V"]),
		Child("insurance-home", "insurance", "Home", "12.1", [], []),
		Child("insurance-vehicle", "insurance", "Car & motorcycle", "12.1", [], []),
		Child("insurance-health", "insurance", "Health insurance & health fund", "12.1", [], ["MUTUALITE", "MUTUALITEIT", "PARTENA", "HELAN", "SOLIDARIS", "CM ", "MLOZ", "DKV"]),
		Child("insurance-life", "insurance", "Life", "12.1", [], []),
		Child("insurance-travel", "insurance", "Travel", "12.1", [], []),
		Child("insurance-liability", "insurance", "Liability", "12.1", [], []),
		Child("bank-fees", "finance", "Bank & payment fees", "12.2", ["KOSTEN", "FRAIS BANCAIRES"], [], "#828282", builtIn: true),

		Root("personal-care", "Personal care & other services", "13", "#6fcf97", [], []),
		Child("care-hair-beauty", "personal-care", "Hairdresser & beauty", "13.1", ["COIFFEUR", "KAPPER", "BARBER", "HAIRDRESSER"], []),
		Child("care-toiletries", "personal-care", "Toiletries & hygiene", "13.1", ["ROSSMANN", "BOOTS", "DOUGLAS", "SEPHORA"], ["KRUIDVAT", "DI ", "ICI PARIS"]),
		Child("care-accessories", "personal-care", "Jewellery, bags & accessories", "13.2", [], []),
		Child("care-social", "personal-care", "Childcare & elderly care", "13.3", ["CRECHE", "KINDEROPVANG", "CHILDCARE"], []),
		Child("care-other", "personal-care", "Other services", "13.9", [], []),

		Root("shopping", "Shops (purpose not specified)", null, "#eb5757", ["AMAZON", "AMZN", "BOL.COM", "ALIEXPRESS", "TEMU", "VINTED", "ACTION", "HEMA"], []),

		Root("income", "Income", null, "#27ae60", [], [], CategoryType.Income, builtIn: true),
		Child("income-salary", "income", "Salary", null, [], ["SALARY", "SALAIRE", "WEDDE"]),
		Child("refunds", "income", "Refunds", null, [], [], "#6fcf97", builtIn: true),
		Child("income-other", "income", "Other income", null, [], []),
		Root("savings", Analysis.SavingsName, null, "#9b51e0", [], [], CategoryType.Savings, builtIn: true),
		Root("own-transfers", Analysis.OwnTransfersName, null, "#bdbdbd", ["REVOLUT", "WISE", "N26"], [], CategoryType.Transfer, builtIn: true),
		Root("loan-received", CategoryDefaults.LoanReceivedName, null, CategoryDefaults.LoanReceivedColor, [], [], CategoryType.Transfer, builtIn: true),
		Root("loan", "Loan repayments", null, "#c0392b", [], [], builtIn: true),
		Root("taxes", "Taxes & government", null, "#636e72", [], []),
		Child("taxes-income", "taxes", "Income tax", null, [], ["SPF FINANCES", "FOD FINANCIEN"]),
		Child("taxes-local", "taxes", "Local & regional taxes", null, [], ["BRUXELLES FISCALITE", "COMMUNE", "GEMEENTE"]),
		Child("taxes-fines", "taxes", "Fines", null, ["AMENDE", "BOETE"], []),
		Root("gifts", "Gifts & donations", null, "#e84393", [], []),
		Child("gifts-given", "gifts", "Gifts", null, [], []),
		Child("gifts-charity", "gifts", "Charity", null, ["UNICEF", "MSF", "RED CROSS", "CROIX-ROUGE", "RODE KRUIS", "MEDECINS SANS FRONTIERES", "ARTSEN ZONDER GRENZEN"], []),
		Root("cash", "Cash withdrawals", null, "#f2c94c", ["GELDOPNAME", "RETRAIT", "CASH", "ATM"], ["BANCONTACT GELDAUTOMAAT"], builtIn: true),
		Root("transfers", "Transfers to others", null, "#56ccf2", [], [], builtIn: true),
		Root("bills", "Unsorted direct debits", null, "#2d9cdb", [], [], builtIn: true),
		Root("other", "Other", null, "#a0a7b4", [], [], builtIn: true),
	];
	#endregion

	#region Private Helpers
	/// <summary>
	/// Describes a top-level node. Spending roots are COICOP divisions and default to <see cref="CategoryType.Expense"/>.
	/// </summary>
	private static CategorySeedNode Root(string id, string name, string? coicop, string color, string[] core, string[] belgium,
		CategoryType type = CategoryType.Expense, bool builtIn = false) =>
		new(id, null, name, coicop, core, belgium, type, color, builtIn);

	/// <summary>
	/// Describes a child node, which takes the type and, unless given, the colour of its root.
	/// </summary>
	private static CategorySeedNode Child(string id, string parentId, string name, string? coicop, string[] core, string[] belgium,
		string? color = null, bool builtIn = false) =>
		new(id, parentId, name, coicop, core, belgium, Color: color, BuiltIn: builtIn);
	#endregion
}

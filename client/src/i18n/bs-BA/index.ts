// Default language. Other languages must have exactly these keys (see MessageSchema in boot/i18n.ts).
// vue-i18n syntax: {name} is a placeholder; write a literal @ { } | as {'@'} etc.
const messages = {
  app: {
    name: 'ERP',
  },
  common: {
    loading: 'Učitavanje…',
    search: 'Pretraga',
    retry: 'Pokušaj ponovo',
    yes: 'Da',
    no: 'Ne',
    comingSoon: 'Uskoro',
  },
  auth: {
    title: 'Prijava',
    subtitle: 'Prijavite se svojim korisničkim računom',
    email: 'E-mail',
    password: 'Lozinka',
    signIn: 'Prijavi se',
    signOut: 'Odjava',
    sessionExpired: 'Sesija je istekla. Prijavite se ponovo.',
    errors: {
      invalidCredentials: 'Pogrešan e-mail ili lozinka.',
      lockedOut:
        'Račun je privremeno zaključan zbog previše neuspjelih prijava. Pokušajte kasnije.',
      tooManyRequests: 'Previše pokušaja. Sačekajte minutu pa pokušajte ponovo.',
      unavailable: 'Server nije dostupan. Provjerite vezu i pokušajte ponovo.',
      generic: 'Prijava nije uspjela.',
    },
  },
  validation: {
    required: 'Obavezno polje',
  },
  layout: {
    home: 'Početna',
    toggleNavigation: 'Prikaži ili sakrij navigaciju',
    language: 'Jezik',
    company: 'Firma',
    userMenu: 'Korisnički meni',
    noCompany: 'Vaš račun još nije član nijedne firme.',
  },
  home: {
    title: 'Dobro došli, {name}',
    subtitle: 'Odaberite modul u navigaciji.',
  },
  modules: {
    core: {
      title: 'Šifarnici',
      partners: 'Poslovni partneri',
      articles: 'Artikli',
      unitsOfMeasure: 'Jedinice mjere',
    },
    sales: {
      title: 'Prodaja',
    },
    purchasing: {
      title: 'Nabavka',
    },
    inventory: {
      title: 'Skladište',
    },
    accounting: {
      title: 'Finansije',
    },
    administration: {
      title: 'Administracija',
      users: 'Korisnici',
      roles: 'Uloge',
    },
  },
  partners: {
    title: 'Poslovni partneri',
    searchHint: 'Šifra ili naziv',
    columns: {
      code: 'Šifra',
      name: 'Naziv',
      taxId: 'JIB',
      vatNumber: 'PDV broj',
      city: 'Mjesto',
      type: 'Vrsta',
      status: 'Status',
    },
    customer: 'Kupac',
    supplier: 'Dobavljač',
    active: 'Aktivan',
    inactive: 'Neaktivan',
    empty: 'Nema poslovnih partnera.',
    loadFailed: 'Učitavanje poslovnih partnera nije uspjelo.',
    rowsPerPage: 'Redova po stranici',
    pagination: '{first}–{last} od {total}',
  },
  placeholder: {
    description: 'Ovaj dio aplikacije je u izradi.',
  },
  errors: {
    notFound: 'Stranica nije pronađena.',
    forbidden: 'Nemate pravo pristupa ovoj stranici.',
    goHome: 'Na početnu',
  },
  languages: {
    'bs-BA': 'Bosanski',
    'en-US': 'English',
  },
};

export default messages;

export type Messages = typeof messages;

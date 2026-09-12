import {ChangeDetectionStrategy, Component, OnInit} from '@angular/core';
import {ActivatedRoute} from '@angular/router';

import {environment} from '../../../environments/environment';

type CatalogLegalPage = 'legal' | 'privacy' | 'rights' | 'cookies' | 'accessibility';

type LegalLink = Readonly<{
  label: string;
  href: string;
  external?: boolean;
  testId?: string;
}>;

type LegalSection = Readonly<{
  title: string;
  paragraphs: readonly string[];
  bullets?: readonly string[];
  note?: string;
  links?: readonly LegalLink[];
}>;

type LegalPageViewModel = Readonly<{
  title: string;
  eyebrow: string;
  intro: string;
  sections: readonly LegalSection[];
}>;

const ASSOCIATION_NAME = "Vole Papillon d'Amour";
const CONTACT_EMAIL = 'volepapillondamour@sfr.fr';
const POSTAL_ADDRESS = '46 route de Saint Marcellin, 42170 Saint-Just-Saint-Rambert, France';
const CATALOG_URL = 'https://livres.volepapillondamour.fr';
const PUBLIC_WEBSITE_URL = 'https://volepapillondamour.fr';
const HOSTING_PROVIDER_NAME = 'Microsoft Azure';
const HOSTING_PROVIDER_URL = 'https://azure.microsoft.com/products/container-apps/';
const HOSTING_POSTAL_ADDRESS = 'Microsoft Ireland Operations Limited, One Microsoft Place, South County Business Park, Leopardstown, Dublin 18, D18 P521, Irlande';
const CLARITY_PROJECT_ID = environment.clarityProjectId;
const GOOGLE_ANALYTICS_MEASUREMENT_ID = environment.googleAnalyticsMeasurementId;
const CLARITY_ID_LABEL = isConfigured(CLARITY_PROJECT_ID)
  ? `Identifiant de projet Clarity : ${CLARITY_PROJECT_ID}.`
  : 'Identifiant de projet Clarity : configuré au moment du déploiement.';
const GOOGLE_ANALYTICS_ID_LABEL = isConfigured(GOOGLE_ANALYTICS_MEASUREMENT_ID)
  ? `Identifiant de mesure GA4 : ${GOOGLE_ANALYTICS_MEASUREMENT_ID}.`
  : 'Identifiant de mesure GA4 : configuré au moment du déploiement.';
const RIGHTS_REQUEST_HREF = `mailto:${CONTACT_EMAIL}?subject=${encodeURIComponent('Demande RGPD — mes données')}&body=${encodeURIComponent(
  'Bonjour,\n\nJe souhaite exercer le droit suivant : accès / rectification / effacement / limitation / opposition / portabilité.\nAdresse e-mail utilisée pour mon compte :\n\nMerci.',
)}`;
const CNIL_COMPLAINT_URL = 'https://www.cnil.fr/fr/plaintes';
const LAST_UPDATED_LABEL = '12 septembre 2026';

const LEGAL_PAGES: Record<CatalogLegalPage, LegalPageViewModel> = {
  legal: {
    title: 'Mentions légales.',
    eyebrow: 'Informations légales',
    intro: `Le site ${CATALOG_URL} est le catalogue public de la bourse aux livres de l'association ${ASSOCIATION_NAME}.`,
    sections: [
      {
        title: 'Éditeur du catalogue',
        paragraphs: [
          `Le catalogue est édité par ${ASSOCIATION_NAME}, association loi 1901 à but non lucratif.`,
          'Dénomination déclarée : « VOLE, PAPILLON D\'AMOUR ». L’association a été déclarée le 15 février 2010 à la sous-préfecture de Montbrison et publiée au Journal officiel des associations le 6 mars 2010.',
        ],
        bullets: [
          'Numéro RNA : W421002487.',
          `Adresse postale : ${POSTAL_ADDRESS}.`,
          `Contact : ${CONTACT_EMAIL} — ${'06 10 83 52 93'}.`,
          "L'association n'est pas immatriculée au répertoire Sirene et ne dispose donc ni de numéro SIREN ni de numéro SIRET.",
        ],
      },
      {
        title: 'Responsable de publication',
        paragraphs: [
          "La directrice de publication est Corinne Drevet, présidente de l'association.",
          `Toute demande relative au contenu du catalogue peut être adressée à ${CONTACT_EMAIL}.`,
        ],
      },
      {
        title: 'Hébergement',
        paragraphs: [
          `Le catalogue est hébergé sur l'infrastructure Microsoft Azure, au moyen du service Azure Container Apps. Le nom de domaine public est ${CATALOG_URL}.`,
        ],
        bullets: [
          `Hébergeur : ${HOSTING_PROVIDER_NAME}.`,
          `Adresse : ${HOSTING_POSTAL_ADDRESS}.`,
          `Service et informations : ${HOSTING_PROVIDER_URL}.`,
        ],
      },
      {
        title: 'Objet et disponibilité du catalogue',
        paragraphs: [
          'Le catalogue permet de consulter les livres proposés dans le cadre des bourses aux livres de l’association et de préparer une recherche avant l’événement.',
          'Les disponibilités sont indicatives et peuvent changer rapidement. Les livres ne sont ni réservés ni vendus en ligne : le prix, le stock définitif et la remise sont confirmés sur place lors de la bourse aux livres.',
        ],
      },
      {
        title: 'Propriété intellectuelle',
        paragraphs: [
          'Les textes, éléments graphiques, photographies et éléments d’interface produits pour le catalogue restent protégés par le droit d’auteur, sauf mention contraire.',
          'Les métadonnées bibliographiques et les images de couverture peuvent provenir de sources externes et restent soumises aux droits de leurs auteurs, éditeurs ou fournisseurs. Toute reproduction substantielle doit être autorisée par le titulaire des droits concerné.',
        ],
      },
      {
        title: 'Responsabilité',
        paragraphs: [
          "L'association s'efforce de maintenir des informations exactes et à jour, sans pouvoir garantir l'absence totale d'erreur, d'omission ou d'indisponibilité ponctuelle.",
          'Le catalogue est fourni à titre informatif. La présence d’un livre, sa disponibilité et son prix doivent être vérifiés sur place. Chaque visiteur reste responsable de l’usage qu’il fait des informations consultées.',
        ],
      },
      {
        title: 'Contact',
        paragraphs: [
          `Pour toute question sur le catalogue, son contenu ou son fonctionnement, vous pouvez écrire à ${CONTACT_EMAIL}, appeler le 06 10 83 52 93 ou adresser un courrier à ${POSTAL_ADDRESS}.`,
        ],
      },
    ],
  },
  privacy: {
    title: 'Confidentialité.',
    eyebrow: 'Données personnelles',
    intro: 'Cette politique explique quelles données peuvent être traitées par le catalogue, pourquoi elles le sont et comment exercer vos droits.',
    sections: [
      {
        title: 'Consultation publique',
        paragraphs: [
          'La recherche et la consultation des livres sont accessibles sans compte et sans formulaire obligatoire. Une simple consultation ne demande donc pas de renseigner une identité.',
          'Comme tout service web, le fonctionnement et la sécurité peuvent toutefois entraîner le traitement de données techniques limitées dans les journaux de l’hébergeur ou de l’API : adresse IP, date, requête, navigateur ou erreurs techniques selon les réglages d’infrastructure.',
        ],
      },
      {
        title: 'Compte, liste de recherche et alertes',
        paragraphs: [
          'Si vous créez un compte, Microsoft Entra External ID peut traiter votre adresse électronique, votre prénom et votre nom lorsqu’ils sont renseignés, ainsi que les éléments nécessaires à l’authentification. Le catalogue associe ensuite à votre compte les livres suivis dans votre liste de recherche et votre préférence d’alertes. Le catalogue et son API ne conservent pas votre mot de passe.',
          'Ces données servent uniquement à fournir les fonctions de compte, de liste de recherche et d’alerte liées au catalogue. Aucun paiement ni achat en ligne n’est réalisé depuis ce site.',
        ],
        bullets: [
          'Adresse e-mail, prénom et nom renseignés, identifiant technique de compte et éléments de session nécessaires à l’authentification.',
          'Identifiants des éditions ou œuvres ajoutées à la liste de recherche.',
          'Préférence d’activation, de suspension ou de désinscription des alertes, ainsi que les éléments techniques de remise nécessaires au service.',
        ],
      },
      {
        title: "Mesure d'audience après consentement",
        paragraphs: [
          `Microsoft Clarity et Google Analytics 4 ne sont chargés qu'après votre consentement explicite via la bannière de cookies. Ils peuvent alors traiter des données d’usage telles que les pages consultées, les interactions de navigation, le type d’appareil ou de navigateur et des informations techniques associées.`,
          'La configuration du catalogue désactive les signaux publicitaires et la personnalisation publicitaire Google. La mesure reste facultative et votre refus ne limite pas la consultation publique.',
        ],
        bullets: [
          CLARITY_ID_LABEL,
          GOOGLE_ANALYTICS_ID_LABEL,
        ],
      },
      {
        title: 'Carte interactive après consentement',
        paragraphs: [
          'La carte interactive Google Maps est chargée dans la page uniquement après votre consentement dédié. Google peut alors recevoir l’adresse recherchée et les données techniques nécessaires à l’affichage du service.',
          'Sans cet accord, le catalogue affiche l’adresse et conserve un lien volontaire pour ouvrir Maps dans un nouvel onglet.',
        ],
      },
      {
        title: 'Finalités et bases de traitement',
        paragraphs: [
          'Les données sont utilisées pour faire fonctionner le catalogue, protéger le service, gérer les comptes et les alertes demandés, et mesurer l’usage du site lorsque vous l’autorisez.',
        ],
        bullets: [
          'Fonctionnement, sécurité et maintenance : intérêt légitime de l’association.',
          'Compte, liste de recherche et alertes : fourniture de la fonctionnalité demandée et exécution des mesures nécessaires à votre demande.',
          "Mesure d'audience Clarity et GA4 : consentement, retirable à tout moment depuis « Gérer les cookies ».",
        ],
      },
      {
        title: 'Destinataires',
        paragraphs: [
          'Les données sont accessibles aux personnes habilitées de l’association et aux prestataires nécessaires au fonctionnement du service. Elles ne sont pas vendues ni louées à des fins commerciales.',
        ],
        bullets: [
          'Microsoft Azure et les services techniques nécessaires à l’hébergement et à l’exécution du catalogue.',
          'Microsoft Entra External ID pour l’authentification des comptes.',
          'Azure Communication Services pour l’envoi des alertes demandées et le suivi technique de leur remise.',
          'Microsoft Clarity et Google Analytics 4 uniquement si la mesure d’audience est acceptée.',
          'Google Maps uniquement si la carte interactive est acceptée.',
        ],
      },
      {
        title: 'Durées de conservation',
        paragraphs: [
          'Les données de compte et de liste de recherche sont conservées pendant la durée d’utilisation du compte, puis supprimées ou anonymisées lorsque le compte est supprimé, sous réserve des obligations légales et des contraintes de sécurité.',
          'Les journaux techniques et les données de mesure d’audience sont conservés selon les réglages de l’infrastructure et les politiques des prestataires concernés. L’association doit maintenir ces durées dans son registre de traitements et les réviser si les réglages évoluent.',
        ],
        note: 'Les durées opérationnelles exactes doivent être documentées et validées par l’association avant une collecte à grande échelle.',
      },
      {
        title: 'Vos droits',
        paragraphs: [
          `Vous pouvez demander l’accès à vos données, leur rectification, leur effacement, la limitation du traitement ou vous opposer à certains usages selon votre situation. Vous pouvez retirer votre consentement à la mesure d’audience ou à la carte interactive à tout moment.`,
          `Pour connaître la procédure, les délais et les données concernées, consultez la page « Vos données et le RGPD ». Vous pouvez aussi utiliser le parcours de suppression proposé dans votre espace membre.`,
          'Si la réponse apportée ne vous paraît pas satisfaisante, vous pouvez saisir la Commission nationale de l’informatique et des libertés (CNIL).',
        ],
        links: [
          {label: 'Consulter vos données et vos droits', href: '/donnees-personnelles'},
        ],
      },
      {
        title: 'Transferts éventuels',
        paragraphs: [
          'Les prestataires techniques peuvent traiter certaines données en dehors de la France ou de l’Union européenne selon leurs infrastructures et leurs conditions de service. Les garanties et lieux de traitement applicables doivent être suivis dans la documentation contractuelle et le registre de l’association.',
        ],
      },
    ],
  },
  rights: {
    title: 'Vos données et le RGPD.',
    eyebrow: 'Exercer vos droits',
    intro: 'Vous pouvez demander à consulter, corriger, exporter ou supprimer les données liées à votre compte Catalogue. Cette page vous indique quoi demander et comment le faire.',
    sections: [
      {
        title: 'Responsable du traitement',
        paragraphs: [
          `Pour le compte, la liste de recherche et les alertes du catalogue ${CATALOG_URL}, le responsable du traitement est l’association ${ASSOCIATION_NAME}, ${POSTAL_ADDRESS}. Le point de contact pour toute demande est ${CONTACT_EMAIL}.`,
          'Cette page complète la politique de confidentialité et la politique de cookies. Elle ne concerne que les données liées au Catalogue ; les éventuels services tiers ouverts depuis un lien restent soumis à leurs propres informations.',
        ],
      },
      {
        title: 'Quelles données sont liées à mon compte ?',
        paragraphs: [
          'La création du compte utilise votre adresse e-mail, votre prénom et votre nom lorsqu’ils sont renseignés, pour fournir votre espace personnel et vos alertes. Microsoft Entra External ID conserve l’identité nécessaire à la connexion ; le catalogue et son API ne conservent pas votre mot de passe.',
          'Lorsque vous utilisez les fonctions du catalogue, nous pouvons associer à votre compte un identifiant technique, des dates de création et de dernière activité, les éditions ou œuvres suivies, votre préférence d’alerte, l’historique des alertes et les informations techniques de remise d’un e-mail. La consultation publique reste possible sans compte.',
        ],
        bullets: [
          'Aucune adresse postale, aucun téléphone et aucune date de naissance ne sont nécessaires pour ce compte.',
          'Les alertes sont des messages liés au service demandé ; elles ne réservent pas un livre et ne constituent pas une inscription à une prospection commerciale.',
        ],
      },
      {
        title: 'Demander une copie de mes données',
        paragraphs: [
          `Écrivez à ${CONTACT_EMAIL} ou utilisez le bouton ci-dessus. Indiquez « Je demande l’accès à mes données au titre de l’article 15 du RGPD », l’adresse e-mail utilisée pour le compte et, si vous le souhaitez, le périmètre demandé : compte, liste de recherche, alertes ou données techniques.`,
          'La réponse est fournie dans un format électronique courant et par un moyen approprié à la confidentialité des informations. Ne joignez pas de pièce d’identité ou de document sensible dans le premier e-mail. Si un doute raisonnable sur votre identité subsiste, l’association peut demander une vérification proportionnée et vous indiquer un canal sécurisé.',
        ],
      },
      {
        title: 'Les autres droits',
        paragraphs: [
          'Votre demande peut porter sur plusieurs droits. L’association examine la demande au regard de la finalité, de la base juridique du traitement, des obligations de conservation et des droits d’autres personnes.',
        ],
        bullets: [
          'Rectification : corriger une donnée inexacte ou incomplète.',
          'Effacement : demander la suppression des données lorsque les conditions sont réunies.',
          'Limitation : demander que les données soient conservées mais que leur utilisation soit temporairement restreinte.',
          'Opposition : vous opposer à un traitement fondé sur l’intérêt légitime, pour des raisons tenant à votre situation.',
          'Portabilité : recevoir les données que vous avez fournies, dans les cas prévus par le RGPD, dans un format structuré et lisible par machine.',
          'Retrait du consentement : modifier à tout moment vos choix de mesure d’audience ou de carte interactive avec « Gérer les cookies ».',
        ],
      },
      {
        title: 'Supprimer mon compte',
        paragraphs: [
          'Depuis « Mon compte », la demande supprime le profil local, la liste de recherche et l’historique des alertes associés. Elle demande également la suppression de votre identité de connexion Microsoft Entra External ID ; la finalisation peut se poursuivre en arrière-plan si un traitement différé est nécessaire.',
          'Lorsqu’une donnée doit être conservée pour une trace métier ou une obligation légale, l’association retire les éléments permettant de vous identifier et conserve uniquement ce qui est nécessaire. Les données de membre supprimées ne doivent pas rester dans une file d’envoi d’alerte.',
        ],
        links: [
          {label: 'Ouvrir « Mon compte »', href: '/compte'},
        ],
      },
      {
        title: 'Délais et gratuité',
        paragraphs: [
          'L’exercice de ces droits est gratuit, sauf demande manifestement infondée ou excessive dans les limites prévues par le RGPD. Une réponse est apportée dans un délai d’un mois à compter de la réception de la demande. Ce délai peut être prolongé de deux mois si la demande est complexe ou nombreuse ; l’association doit vous en informer dans le premier mois et en expliquer la raison.',
          'Si l’association ne donne pas suite, elle vous explique pourquoi et vous rappelle votre possibilité de saisir la CNIL. Les droits peuvent être limités lorsque la loi, la sécurité du service ou les droits d’une autre personne l’exigent.',
        ],
      },
      {
        title: 'Contact et réclamation',
        paragraphs: [
          `Pour toute demande RGPD, écrivez à ${CONTACT_EMAIL} ou par courrier à ${POSTAL_ADDRESS}. Décrivez le droit exercé et l’adresse e-mail du compte concerné ; n’envoyez pas de secret ni de mot de passe.`,
          'Vous pouvez introduire une réclamation auprès de la Commission nationale de l’informatique et des libertés si vous estimez que vos droits ne sont pas respectés.',
        ],
        links: [
          {label: 'Saisir la CNIL', href: CNIL_COMPLAINT_URL, external: true},
        ],
      },
    ],
  },
  cookies: {
    title: 'Politique de cookies.',
    eyebrow: "Traceurs et mesure d'audience",
    intro: 'Cette page décrit les traceurs utilisés par le catalogue et les choix proposés avant toute mesure d’audience.',
    sections: [
      {
        title: 'Traceurs nécessaires',
        paragraphs: [
          'Le catalogue peut utiliser les mécanismes techniques indispensables à l’affichage, à la sécurité et à la continuité de navigation entre le rendu serveur et le navigateur.',
          'Le choix de consentement est mémorisé dans le stockage local du navigateur sous la clé technique vpd-catalog-cookie-consent. Ce stockage ne sert pas à profiler la navigation et aucun traceur tiers de mesure n’est chargé avant votre choix.',
        ],
      },
      {
        title: 'Google Maps',
        paragraphs: [
          'La carte interactive Google Maps est un contenu fourni par un tiers. Elle est chargée dans la page uniquement après votre consentement dédié à cette catégorie.',
          'Sans ce consentement, la page affiche l’adresse et un bouton volontaire « Afficher la carte Google Maps ». Le lien « Ouvrir dans Maps » permet aussi d’ouvrir directement le service Google Maps dans un nouvel onglet. Vous pouvez refuser la carte sans empêcher la consultation du catalogue.',
        ],
      },
      {
        title: `Microsoft Clarity et Google Analytics 4`,
        paragraphs: [
          'Ces deux outils sont utilisés pour comprendre les parcours de consultation du catalogue et améliorer son ergonomie. Ils peuvent mesurer les pages visitées, les interactions, le type d’appareil ou de navigateur et, pour Clarity, produire des enregistrements de session selon les paramètres du service.',
        ],
        bullets: [
          'Microsoft Clarity : mesure comportementale et compréhension des parcours.',
          'Google Analytics 4 : statistiques de consultation et d’engagement.',
          CLARITY_ID_LABEL,
          GOOGLE_ANALYTICS_ID_LABEL,
          'Aucune personnalisation publicitaire ni signal publicitaire Google n’est activé par la configuration du catalogue.',
        ],
      },
      {
        title: 'Consentement explicite',
        paragraphs: [
          'À votre première visite, la bannière recueille votre consentement explicite et vous permet de tout accepter, de tout refuser ou de personnaliser chaque catégorie. Tant que vous n’avez pas accepté la mesure d’audience, Microsoft Clarity et Google Analytics 4 ne sont ni chargés ni exécutés ; tant que vous n’avez pas accepté la carte interactive, Google Maps n’est pas intégré à la page.',
          'Si vous retirez ensuite l’un de vos accords, le catalogue désactive les chargements futurs correspondants et transmet le refus aux outils déjà chargés lorsque cela est techniquement possible.',
        ],
      },
      {
        title: 'Modifier ou supprimer votre choix',
        paragraphs: [
          'Le lien « Gérer les cookies » présent dans le pied de page permet de rouvrir la bannière et de modifier votre choix à tout moment. Vous pouvez également supprimer le stockage local et les cookies depuis les réglages de votre navigateur.',
          `Pour toute question sur les traceurs, contactez ${CONTACT_EMAIL}.`,
        ],
      },
    ],
  },
  accessibility: {
    title: 'Accessibilité.',
    eyebrow: "Engagement d'amélioration",
    intro: 'L’association souhaite rendre le catalogue consultable par le plus grand nombre. Cette déclaration présente le statut connu et le moyen de signaler une difficulté.',
    sections: [
      {
        title: 'État de conformité',
        paragraphs: [
          'Statut actuel : non conforme déclaré. Aucun audit complet de conformité RGAA n’est documenté à ce jour pour le catalogue, ce qui ne permet pas de publier un taux de conformité fiable.',
        ],
      },
      {
        title: 'Périmètre',
        paragraphs: [
          `Cette déclaration concerne les pages publiques du catalogue ${CATALOG_URL}, notamment la recherche, les fiches de livres, les œuvres, les prochaines dates, les pages légales et le parcours de compte.`,
          'Les services d’authentification et les contenus bibliographiques fournis par des tiers peuvent nécessiter une analyse complémentaire.',
        ],
      },
      {
        title: 'Limitations et amélioration',
        paragraphs: [
          'La liste détaillée des obstacles rencontrés par les utilisateurs doit être formalisée à partir d’un audit et de retours d’usage. Les améliorations sont priorisées au fil des corrections, notamment sur la navigation clavier, les contrastes, les libellés et les petits écrans.',
        ],
      },
      {
        title: 'Signaler une difficulté',
        paragraphs: [
          `Si vous ne parvenez pas à accéder à une information ou à une fonctionnalité, écrivez à ${CONTACT_EMAIL}. Indiquez si possible l’URL, l’appareil, le navigateur et la difficulté rencontrée afin de faciliter le diagnostic.`,
          `Vous pouvez également écrire à l’association à l’adresse suivante : ${POSTAL_ADDRESS}.`,
        ],
      },
    ],
  },
};

@Component({
  selector: 'app-catalog-legal-page',
  standalone: false,
  templateUrl: './legal-page.component.html',
  styleUrls: ['./legal-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LegalPageComponent implements OnInit {
  readonly associationName = ASSOCIATION_NAME;
  readonly contactEmail = CONTACT_EMAIL;
  readonly contactEmailHref = `mailto:${CONTACT_EMAIL}`;
  readonly postalAddress = POSTAL_ADDRESS;
  readonly catalogUrl = CATALOG_URL;
  readonly publicWebsiteUrl = PUBLIC_WEBSITE_URL;
  readonly hostingProviderName = HOSTING_PROVIDER_NAME;
  readonly hostingProviderUrl = HOSTING_PROVIDER_URL;
  readonly lastUpdatedLabel = LAST_UPDATED_LABEL;
  readonly rightsRequestHref = RIGHTS_REQUEST_HREF;

  page: CatalogLegalPage = 'legal';
  content: LegalPageViewModel = LEGAL_PAGES.legal;

  constructor(private readonly route: ActivatedRoute) {}

  ngOnInit(): void {
    const requestedPage: unknown = this.route.snapshot.data['page'] as unknown;
    this.page = isCatalogLegalPage(requestedPage) ? requestedPage : 'legal';
    this.content = LEGAL_PAGES[this.page];
  }
}

function isCatalogLegalPage(value: unknown): value is CatalogLegalPage {
  return value === 'legal'
    || value === 'privacy'
    || value === 'rights'
    || value === 'cookies'
    || value === 'accessibility';
}

function isConfigured(value: string): boolean {
  return value.length > 0 && !value.startsWith('__');
}

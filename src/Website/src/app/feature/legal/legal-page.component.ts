import { Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

import { LEGAL_PAGE_PATHS } from './legal-page-paths';

const ASSOCIATION_NAME = "Vole Papillon d'Amour";
const CONTACT_EMAIL = 'volepapillondamour@sfr.fr';
const POSTAL_ADDRESS = '46 route de Saint Marcellin, 42170 Saint-Just-Saint-Rambert, France';
const PUBLIC_WEBSITE_URL = 'https://volepapillondamour.fr';
const HOSTING_PROVIDER_NAME = 'Render';
const HOSTING_PROVIDER_URL = 'https://render.com';
const CLARITY_TAG_ID = 'nwy66l4uol';
const LAST_UPDATED_LABEL = '18 mai 2026';
const RESPONSABLE_PUBLICATION_PLACEHOLDER = '[A COMPLETER - responsable de publication]';
const RNA_PLACEHOLDER = '[A COMPLETER - numéro RNA de l\'association]';
const SIREN_PLACEHOLDER = '[A COMPLETER - numéro SIREN ou SIRET de l\'association]';
const PHONE_PLACEHOLDER = '[A COMPLETER - numéro de téléphone public si vous souhaitez le diffuser]';
const HOSTING_POSTAL_ADDRESS_PLACEHOLDER = '[A COMPLETER - adresse postale complète de l\'hébergeur Render]';
const RETENTION_PLACEHOLDER = '[A COMPLETER - durées de conservation applicables selon vos pratiques]';
const TRANSFER_PLACEHOLDER = '[A COMPLETER - garanties et lieux exacts des éventuels transferts hors Union européenne]';
const ACCESSIBILITY_LIMITATIONS_PLACEHOLDER = '[A COMPLETER - limitations d\'accessibilité actuellement identifiées]';
const ACCESSIBILITY_PLAN_PLACEHOLDER = '[A COMPLETER - calendrier et priorités d\'amélioration de l\'accessibilité]';

type LegalPagePath = (typeof LEGAL_PAGE_PATHS)[keyof typeof LEGAL_PAGE_PATHS];

type LegalSection = Readonly<{
  title: string;
  paragraphs: readonly string[];
  bullets?: readonly string[];
  note?: string;
}>;

type LegalPageViewModel = Readonly<{
  title: string;
  eyebrow: string;
  intro: string;
  sections: readonly LegalSection[];
}>;

const LEGAL_PAGES: Record<LegalPagePath, LegalPageViewModel> = {
  [LEGAL_PAGE_PATHS.mentionsLegales]: {
    title: 'Mentions légales',
    eyebrow: 'Informations légales',
    intro: 'Cette page présente les informations publiques actuellement disponibles pour le site de l\'association et signale clairement les éléments qui restent à compléter.',
    sections: [
      {
        title: 'Éditeur du site',
        paragraphs: [
          `Le site public ${PUBLIC_WEBSITE_URL} est édité pour l'association ${ASSOCIATION_NAME}.`,
          'Les informations ci-dessous doivent être relues et validées par le bureau de l\'association avant publication définitive.'
        ],
        bullets: [
          `Nom de l'association : ${ASSOCIATION_NAME}`,
          `Adresse postale : ${POSTAL_ADDRESS}`,
          `Adresse email : ${CONTACT_EMAIL}`,
          `Téléphone : ${PHONE_PLACEHOLDER}`,
          `Numéro RNA : ${RNA_PLACEHOLDER}`,
          `Numéro SIREN / SIRET : ${SIREN_PLACEHOLDER}`,
          'Crédits de réalisation : Florian Drevet'
        ]
      },
      {
        title: 'Directeur ou responsable de publication',
        paragraphs: [
          `Le directeur ou la directrice de publication à mentionner est : ${RESPONSABLE_PUBLICATION_PLACEHOLDER}.`,
          'Cette personne doit être identifiée nominativement pour que la page soit complète.'
        ]
      },
      {
        title: 'Hébergeur',
        paragraphs: [
          `Le site est hébergé sur l'infrastructure de ${HOSTING_PROVIDER_NAME}.`,
          'Le domaine public de l\'association est servi via cette infrastructure, avec une diffusion technique possible sur un sous-domaine Render.'
        ],
        bullets: [
          `Hébergeur : ${HOSTING_PROVIDER_NAME}`,
          `Site web de l'hébergeur : ${HOSTING_PROVIDER_URL}`,
          `Adresse postale de l'hébergeur : ${HOSTING_POSTAL_ADDRESS_PLACEHOLDER}`
        ]
      },
      {
        title: 'Propriété intellectuelle',
        paragraphs: [
          'Sauf mention contraire, les textes, photographies, éléments graphiques et contenus publiés sur ce site restent protégés par le droit d\'auteur et les droits afférents.',
          'Toute reproduction, adaptation ou réutilisation substantielle doit faire l\'objet d\'une autorisation préalable de l\'association ou des titulaires des droits concernés.'
        ]
      },
      {
        title: 'Responsabilité',
        paragraphs: [
          'L\'association s\'efforce de fournir des informations aussi exactes et à jour que possible, sans pouvoir garantir l\'absence totale d\'erreur, d\'omission ou d\'indisponibilité ponctuelle.',
          'Les contenus sont publiés à titre informatif. Chaque internaute reste responsable de l\'usage qu\'il fait des informations consultées sur le site.'
        ]
      },
      {
        title: 'Contact',
        paragraphs: [
          `Pour toute question au sujet du site ou de son contenu, vous pouvez écrire à ${CONTACT_EMAIL} ou adresser un courrier à ${POSTAL_ADDRESS}.`,
          'Si vous nous contactez pour exercer un droit lié à vos données personnelles, indiquez clairement l\'objet de votre demande pour faciliter son traitement.'
        ]
      }
    ]
  },
  [LEGAL_PAGE_PATHS.politiqueConfidentialite]: {
    title: 'Politique de confidentialité',
    eyebrow: 'Données personnelles',
    intro: 'Cette politique explique, en langage clair, quelles données peuvent être traitées sur le site public et dans quel but. Elle sera à compléter à mesure que la gouvernance et les durées de conservation seront validées.',
    sections: [
      {
        title: 'Quelles données peuvent être traitées ?',
        paragraphs: [
          'Le site ne propose pas actuellement de formulaire de contact intégré. En pratique, les données traitées concernent surtout la navigation et les messages que vous choisissez d\'envoyer directement à l\'association.',
          'Les traitements peuvent inclure des données techniques limitées, strictement liées à l\'affichage du site et à la mesure d\'usage.'
        ],
        bullets: [
          'Données de navigation : pages consultées, date et heure de consultation, type de navigateur, système d\'exploitation et informations techniques comparables.',
          `Données de mesure d'audience : interactions de navigation et signaux collectés par l'outil Microsoft Clarity lorsque son script est chargé sur le site.`,
          `Données de contact volontaire : contenu du message, adresse email expéditrice et pièces éventuellement jointes lorsque vous écrivez à ${CONTACT_EMAIL}.`
        ]
      },
      {
        title: 'Pourquoi ces données sont-elles utilisées ?',
        paragraphs: [
          'Les données sont utilisées pour faire fonctionner le site, comprendre son utilisation et répondre aux demandes reçues par email.',
          'Les bases juridiques ci-dessous sont formulées simplement pour rester compréhensibles par tous.'
        ],
        bullets: [
          'Fonctionnement et sécurité du site : intérêt légitime de l\'association à maintenir un site accessible et stable.',
          'Réponse à un message reçu par email : traitement nécessaire pour répondre à votre demande.',
          `Mesure d'audience et amélioration du contenu : consentement recueilli via la bannière de cookies affichée lors de votre première visite, modifiable à tout moment depuis le lien « Gérer les cookies » en pied de page.`
        ]
      },
      {
        title: 'Qui reçoit les données ?',
        paragraphs: [
          'Les données sont destinées aux personnes habilitées au sein de l\'association et, lorsque cela est nécessaire, à ses prestataires techniques.',
          'Aucune cession commerciale de données personnelles n\'est annoncée sur ce site.'
        ],
        bullets: [
          'Membres ou bénévoles habilités de l\'association pour le suivi des messages et du site.',
          `Prestataires techniques d'hébergement et d'analyse, notamment ${HOSTING_PROVIDER_NAME} et Microsoft Clarity selon la configuration réellement utilisée.`
        ]
      },
      {
        title: 'Combien de temps les données sont-elles conservées ?',
        paragraphs: [
          'Les durées doivent être fixées et documentées de manière plus précise.',
          'En attendant, elles sont laissées avec un marqueur explicite pour validation interne.'
        ],
        bullets: [
          `Messages reçus par email : ${RETENTION_PLACEHOLDER}`,
          `Journaux techniques et sécurité : ${RETENTION_PLACEHOLDER}`,
          `Données de mesure d'audience : ${RETENTION_PLACEHOLDER}`
        ]
      },
      {
        title: 'Quels sont vos droits ?',
        paragraphs: [
          'Vous pouvez demander l\'accès à vos données, leur rectification, leur effacement, la limitation du traitement ou vous opposer à certains usages selon votre situation.',
          'Vous pouvez également retirer votre consentement lorsqu\'il constitue la base du traitement, sans effet rétroactif sur ce qui a déjà été réalisé.'
        ],
        bullets: [
          `Point de contact : ${CONTACT_EMAIL}`,
          'Si la réponse apportée ne vous paraît pas satisfaisante, vous pouvez également contacter la CNIL.'
        ]
      },
      {
        title: 'Hébergement et transferts éventuels',
        paragraphs: [
          `Le site est hébergé chez ${HOSTING_PROVIDER_NAME}. Selon les réglages retenus pour l'hébergement et la mesure d'audience, certaines données techniques peuvent être traitées en dehors de la France.`,
          'Les lieux exacts de traitement et les garanties associées doivent encore être confirmés.'
        ],
        note: TRANSFER_PLACEHOLDER
      }
    ]
  },
  [LEGAL_PAGE_PATHS.politiqueCookies]: {
    title: 'Politique de cookies',
    eyebrow: 'Traceurs et mesure d\'audience',
    intro: 'Cette page décrit les traceurs susceptibles d\'être utilisés sur le site public. Elle distingue les éléments strictement techniques des outils de mesure d\'audience réellement présents dans le code.',
    sections: [
      {
        title: 'À quoi servent les cookies et traceurs ?',
        paragraphs: [
          'Un cookie ou un traceur est un petit fichier ou un mécanisme technique utilisé pour mémoriser une information, faciliter l\'affichage du site ou mesurer son usage.',
          'Tous les traceurs ne servent pas au même objectif. Certains sont strictement nécessaires, d\'autres relèvent de la mesure d\'audience.'
        ]
      },
      {
        title: 'Cookies techniques',
        paragraphs: [
          'Le site peut utiliser des traceurs strictement nécessaires à l\'affichage des pages, à la stabilité du service, à la sécurité et au bon déroulement des parcours de navigation.',
          'Ces éléments techniques n\'ont pas vocation à profiler les visiteurs à des fins commerciales.'
        ],
        bullets: [
          'Affichage correct des pages et des ressources associées.',
          'Sécurisation minimale du service et limitation de certains comportements abusifs.',
          'Maintien du service entre le rendu serveur et l\'hydratation côté navigateur lorsque cela est nécessaire.'
        ]
      },
      {
        title: 'Mesure d\'audience et Microsoft Clarity',
        paragraphs: [
          `Microsoft Clarity n'est chargé qu'après votre consentement explicite, donné via la bannière de cookies affichée lors de votre première visite. L'identifiant de balise utilisé est ${CLARITY_TAG_ID}.`,
          'Cet outil peut collecter des informations d\'usage comme les pages visitées, les interactions de navigation, des informations sur le navigateur et l\'appareil utilisé afin d\'aider à comprendre la consultation du site.'
        ],
        bullets: [
          'Outil identifié dans le code : Microsoft Clarity.',
          `Identifiant de balise utilisé : ${CLARITY_TAG_ID}.`,
          'Finalité annoncée : mesure d\'audience et compréhension des parcours de navigation.',
          'Déclenchement : uniquement après acceptation, totale ou partielle, des cookies de mesure d\'audience.'
        ]
      },
      {
        title: 'Consentement et paramétrage actuel',
        paragraphs: [
          "Une bannière de consentement s'affiche lors de votre première visite. Elle vous permet de tout accepter, de tout refuser, ou de personnaliser votre choix pour la mesure d'audience.",
          'Tant que vous n\'avez pas donné votre accord, Microsoft Clarity n\'est ni chargé ni exécuté : aucun cookie de mesure d\'audience n\'est déposé avant votre consentement.'
        ],
        bullets: [
          'Votre choix est mémorisé dans votre navigateur (stockage local) afin de ne pas vous solliciter à chaque visite.',
          'Vous pouvez modifier votre choix à tout moment via le lien « Gérer les cookies » en pied de page.'
        ]
      },
      {
        title: 'Comment gérer vos choix ?',
        paragraphs: [
          'Vous pouvez revenir sur votre choix à tout moment, ou limiter et supprimer certains cookies depuis les réglages de votre navigateur (ce qui peut dégrader certaines fonctions techniques).',
          `Pour toute question sur les traceurs utilisés, vous pouvez contacter l'association à ${CONTACT_EMAIL}.`
        ],
        bullets: [
          'Utilisez le lien « Gérer les cookies » en pied de page pour rouvrir la bannière et modifier votre choix.',
          'Consultez les paramètres de confidentialité de votre navigateur.',
          'Supprimez les cookies déjà déposés si vous souhaitez repartir avec une navigation vierge.'
        ]
      }
    ]
  },
  [LEGAL_PAGE_PATHS.accessibilite]: {
    title: 'Accessibilité',
    eyebrow: 'Engagement d\'amélioration',
    intro: 'Le site public n\'est pas encore déclaré conforme aux exigences d\'accessibilité. Cette page expose l\'état actuel, le périmètre visé et les prochains éléments à documenter.',
    sections: [
      {
        title: 'État de conformité',
        paragraphs: [
          'Statut actuel : non conforme.',
          'Aucun audit complet de conformité n\'est documenté à ce jour pour le site public, ce qui empêche de publier une déclaration plus précise.'
        ]
      },
      {
        title: 'Périmètre concerné',
        paragraphs: [
          'Cette déclaration vise les pages publiques accessibles sur le site Vole Papillon d\'Amour, y compris les pages d\'information, d\'actualité, d\'événements et les nouvelles pages légales.',
          'Les contenus tiers, services externes et documents qui ne sont pas maîtrisés directement par l\'association peuvent nécessiter une analyse séparée.'
        ],
        bullets: [
          `Domaine principal : ${PUBLIC_WEBSITE_URL}`,
          'Pages statiques et contenus éditoriaux du site public.',
          'Composants partagés et navigation visible côté visiteur.'
        ]
      },
      {
        title: 'Limitations connues',
        paragraphs: [
          'La liste détaillée des obstacles rencontrés par les utilisateurs doit encore être formalisée.',
          'Les limitations déjà identifiées ou à confirmer doivent être ajoutées ici au fil des audits internes.'
        ],
        bullets: [
          ACCESSIBILITY_LIMITATIONS_PLACEHOLDER
        ]
      },
      {
        title: 'Signaler une difficulté',
        paragraphs: [
          `Si vous rencontrez un problème d'accès à une information ou à une fonctionnalité, écrivez à ${CONTACT_EMAIL}.`,
          `Vous pouvez également adresser un courrier à ${POSTAL_ADDRESS}. Merci de préciser la page concernée, votre équipement et la difficulté constatée.`
        ]
      },
      {
        title: 'Engagement d\'amélioration',
        paragraphs: [
          'L\'association s\'engage à améliorer progressivement l\'expérience de consultation pour tous les visiteurs.',
          'Les prochaines étapes doivent être priorisées et datées afin de rendre le suivi plus concret.'
        ],
        note: ACCESSIBILITY_PLAN_PLACEHOLDER
      }
    ]
  }
};

@Component({
  selector: 'app-legal-page',
  templateUrl: './legal-page.component.html',
  styleUrl: './legal-page.component.scss'
})
export class LegalPageComponent {
  private readonly legalPagePath = inject(ActivatedRoute).snapshot.data['legalPagePath'] as LegalPagePath | undefined;

  protected readonly associationName = ASSOCIATION_NAME;
  protected readonly contactEmail = CONTACT_EMAIL;
  protected readonly contactEmailHref = `mailto:${CONTACT_EMAIL}`;
  protected readonly postalAddress = POSTAL_ADDRESS;
  protected readonly publicWebsiteUrl = PUBLIC_WEBSITE_URL;
  protected readonly hostingProviderName = HOSTING_PROVIDER_NAME;
  protected readonly hostingProviderUrl = HOSTING_PROVIDER_URL;
  protected readonly lastUpdatedLabel = LAST_UPDATED_LABEL;
  protected readonly page = LEGAL_PAGES[this.legalPagePath ?? LEGAL_PAGE_PATHS.mentionsLegales];
}
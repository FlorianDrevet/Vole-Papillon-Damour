import { VpdEventModel } from '../models/vpdEvent.model';

/**
 * Adresse de l'évènement sur une seule ligne ("12 rue des Fleurs, 42170 Saint-Just").
 * Les champs sont saisis séparément côté BackOffice : le recollage est fait ici plutôt
 * que dans chaque gabarit, la page de détail ayant besoin de l'adresse à plusieurs
 * endroits (ligne "Lieu" et lien d'itinéraire).
 */
export function formatEventAddress(event: VpdEventModel): string {
  const street = [event.roadNumber, event.road].filter(Boolean).join(' ').trim();
  const city = [event.cityCode, event.city].filter(Boolean).join(' ').trim();
  return [street, city].filter(Boolean).join(', ');
}

/**
 * Lien "itinéraire" vers Google Maps. On passe par une recherche d'adresse plutôt que
 * par des coordonnées : l'API ne fournit pas de latitude/longitude.
 */
export function eventMapsUrl(event: VpdEventModel): string {
  return `https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(formatEventAddress(event))}`;
}

/**
 * URL d'embed Google Maps pour la carte affichée dans la fiche d'un événement.
 * Le domaine est fixe et seule l'adresse issue du modèle est encodée.
 */
export function eventMapsEmbedUrl(event: VpdEventModel): string {
  return `https://www.google.com/maps?q=${encodeURIComponent(formatEventAddress(event))}&hl=fr&z=15&output=embed`;
}

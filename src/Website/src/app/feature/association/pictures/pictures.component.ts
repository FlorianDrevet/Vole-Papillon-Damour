import { Component } from '@angular/core';

import { PHOTO_ALBUM_CATEGORIES, PHOTO_ALBUMS, PHOTO_ALBUMS_ROUTE } from './photo-album-catalog';

type VideoAsset = Readonly<{
  title: string;
  eyebrow: string;
  description: string;
  source: string;
  downloadName: string;
  mimeType: string;
  poster: string;
}>;

@Component({
    selector: 'app-pictures',
    templateUrl: './pictures.component.html',
    standalone: false
})
export class PicturesComponent {
  readonly albumCategories = PHOTO_ALBUM_CATEGORIES;
  readonly photoAlbums = PHOTO_ALBUMS;
  readonly photoAlbumsRoute = PHOTO_ALBUMS_ROUTE;

  readonly videos: readonly VideoAsset[] = [
    {
      title: 'Maxence · 0–4 ans',
      eyebrow: 'Le fil de sa vie',
      description: 'Les premières années, en musique.',
      source: 'videos/association/maxence-0-4-ans.mp4',
      downloadName: 'maxence-0-4-ans.mp4',
      mimeType: 'video/mp4',
      poster: 'images/Association/Gallery/maxence-2004-2010-01.jpg',
    },
    {
      title: 'Maxence · 5–7 ans',
      eyebrow: 'Le fil de sa vie',
      description: 'Les années d’enfance et les sourires qui restent.',
      source: 'videos/association/maxence-5-7-ans.mp4',
      downloadName: 'maxence-5-7-ans.mp4',
      mimeType: 'video/mp4',
      poster: 'images/Association/Gallery/maxence-2004-2010-02.jpg',
    },
    {
      title: 'Maxence · 8–10 ans',
      eyebrow: 'Le fil de sa vie',
      description: 'Une nouvelle séquence de souvenirs en images.',
      source: 'videos/association/maxence-8-10-ans.mp4',
      downloadName: 'maxence-8-10-ans.mp4',
      mimeType: 'video/mp4',
      poster: 'images/MaxencesHistory/2009/MaxenceBirthdayFiveYears.jpg',
    },
    {
      title: 'Maxence · 11–15 ans',
      eyebrow: 'Le fil de sa vie',
      description: 'L’adolescence, les passions et le chemin parcouru.',
      source: 'videos/association/maxence-11-15-ans.mp4',
      downloadName: 'maxence-11-15-ans.mp4',
      mimeType: 'video/mp4',
      poster: 'images/Association/Gallery/maxence-2011-2019-01.jpg',
    },
    {
      title: 'Maxence · 16–19 ans',
      eyebrow: 'Le fil de sa vie',
      description: 'Les années qui précèdent l’entrée dans la vie adulte.',
      source: 'videos/association/maxence-16-19-ans.mp4',
      downloadName: 'maxence-16-19-ans.mp4',
      mimeType: 'video/mp4',
      poster: 'images/Association/Gallery/maxence-2011-2019-03.jpg',
    },
    {
      title: 'Les anniversaires en musique',
      eyebrow: 'Famille',
      description: 'Un montage consacré aux anniversaires de Maxence.',
      source: 'videos/association/anniversaires.mp4',
      downloadName: 'anniversaires-maxence.mp4',
      mimeType: 'video/mp4',
      poster: 'images/Association/Gallery/anniversaires-20-ans.jpg',
    },
    {
      title: 'Une vie pas comme les autres',
      eyebrow: 'Témoignage',
      description: 'Un film sur le quotidien, les défis et les forces de Maxence.',
      source: 'videos/association/une-vie-pas-comme-les-autres.mp4',
      downloadName: 'une-vie-pas-comme-les-autres.mp4',
      mimeType: 'video/mp4',
      poster: 'images/Association/Gallery/maxence-2020-2026-01.jpg',
    },
    {
      title: 'Vole, Papillon d’amour',
      eyebrow: 'L’association',
      description: 'Le film qui raconte le combat de Maxence.',
      source: 'videos/association/vole-papillon-damour.mp4',
      downloadName: 'vole-papillon-damour.mp4',
      mimeType: 'video/mp4',
      poster: 'images/Association/don-livre.jpg',
    },
    {
      title: 'Les activités de l’association',
      eyebrow: 'L’association',
      description: 'Les actions, les rencontres et les projets portés ensemble.',
      source: 'videos/association/activites-association.mp4',
      downloadName: 'activites-association.mp4',
      mimeType: 'video/mp4',
      poster: 'images/Association/don-livre4.jpg',
    },
    {
      title: 'Famille, amis et bénévoles',
      eyebrow: 'L’association',
      description: 'Celles et ceux qui font vivre cette histoire au quotidien.',
      source: 'videos/association/famille-amis-benevoles.mp4',
      downloadName: 'famille-amis-benevoles.mp4',
      mimeType: 'video/mp4',
      poster: 'images/Association/Gallery/celebrites-yannick-noah.jpg',
    },
    {
      title: 'Dans les coulisses de la maladie',
      eyebrow: 'Témoignage',
      description: 'Un regard plus intime sur le parcours médical de Maxence.',
      source: 'videos/association/coulisses-maladie.mp4',
      downloadName: 'coulisses-maladie.mp4',
      mimeType: 'video/mp4',
      poster: 'images/Association/Gallery/maxence-2004-2010-01.jpg',
    },
    {
      title: 'Le clip',
      eyebrow: 'Souvenir',
      description: 'Une chanson et un clip avec la participation de Michael Jones.',
      source: 'videos/association/clip.mp4',
      downloadName: 'clip-maxence.mp4',
      mimeType: 'video/mp4',
      poster: 'images/Association/Gallery/maxence-2020-2026-02.jpg',
    },
    {
      title: 'Souvenir de l’hôpital',
      eyebrow: 'Souvenir',
      description: 'Une vidéo conservée parmi les souvenirs de l’hôpital.',
      source: 'videos/association/souvenir-hopital.mp4',
      downloadName: 'souvenir-hopital.mp4',
      mimeType: 'video/mp4',
      poster: 'images/MaxencesHistory/2004/MachineHospital.jpg',
    },
    {
      title: '31 décembre 2010',
      eyebrow: 'Souvenir · format original',
      description: 'Le fichier original de cette archive familiale est également disponible au téléchargement.',
      source: 'videos/association/31-decembre-2010.mod',
      downloadName: '31-decembre-2010.mod',
      mimeType: 'video/mpeg',
      poster: 'images/Association/Gallery/maxence-2004-2010-03.jpg',
    },
  ];

}

import { Component, HostListener } from '@angular/core';

type PhotoAlbum = Readonly<{
  title: string;
  eyebrow: string;
  description: string;
  photoCount: number;
  previewPhotos: readonly string[];
  historyRoute?: string;
}>;

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
  readonly donationPhotos: string[] = [
    'images/Association/don-livre.jpg',
    'images/Association/don-livre2.jpg',
    'images/Association/don-livre3.jpg',
    'images/Association/don-livre4.jpg',
    'images/Association/don-livre5.jpg',
    'images/Association/don-livre6.jpg',
    'images/Association/don-livre7.jpg',
    'images/Association/don-livre8.jpg',
    'images/Association/don-dvd.jpg',
  ];

  private readonly albumCatalog: readonly PhotoAlbum[] = [
    {
      title: 'Bourse aux livres',
      eyebrow: 'L’association · album simple',
      description: 'Les livres, les cartons et les visages derrière chaque collecte.',
      photoCount: this.donationPhotos.length,
      previewPhotos: this.donationPhotos,
    },
    {
      title: 'Maxence · 2004–2010',
      eyebrow: 'Le récit de Maxence · 231 photos',
      description: 'Les premières années, de la naissance aux souvenirs de famille.',
      photoCount: 231,
      previewPhotos: [
        'images/Association/Gallery/maxence-2004-2010-01.jpg',
        'images/Association/Gallery/maxence-2004-2010-02.jpg',
        'images/Association/Gallery/maxence-2004-2010-03.jpg',
      ],
      historyRoute: '/maxence/histoire',
    },
    {
      title: 'Maxence · 2011–2019',
      eyebrow: 'Le récit de Maxence · 145 photos',
      description: 'L’école, les hospitalisations et les progrès qui ouvrent la suite.',
      photoCount: 145,
      previewPhotos: [
        'images/Association/Gallery/maxence-2011-2019-01.jpg',
        'images/Association/Gallery/maxence-2011-2019-02.jpg',
        'images/Association/Gallery/maxence-2011-2019-03.jpg',
      ],
      historyRoute: '/maxence/histoire',
    },
    {
      title: 'Maxence · 2020–2026',
      eyebrow: 'Le récit de Maxence · 215 photos',
      description: 'Les années lycée, les études supérieures et une nouvelle autonomie.',
      photoCount: 215,
      previewPhotos: [
        'images/Association/Gallery/maxence-2020-2026-01.jpg',
        'images/Association/Gallery/maxence-2020-2026-02.jpg',
        'images/Association/Gallery/maxence-2020-2026-03.jpg',
      ],
      historyRoute: '/maxence/histoire',
    },
    {
      title: 'Anniversaires',
      eyebrow: 'Famille · 88 photos',
      description: 'Des fêtes, des décors et les petits rituels qui deviennent de grands souvenirs.',
      photoCount: 88,
      previewPhotos: ['images/Association/Gallery/anniversaires-20-ans.jpg'],
    },
    {
      title: 'Célébrités rencontrées',
      eyebrow: 'Rencontres · 15 photos',
      description: 'Les rencontres qui ont marqué les actions et les années de l’association.',
      photoCount: 15,
      previewPhotos: ['images/Association/Gallery/celebrites-yannick-noah.jpg'],
    },
  ];

  readonly photoAlbums: readonly PhotoAlbum[] = [
    ...this.albumCatalog.slice(1),
    this.albumCatalog[0],
  ];

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
      description: 'Le film qui raconte l’association et son combat.',
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
      description: 'Un clip familial conservé dans les archives.',
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

  selectedAlbum: PhotoAlbum | null = null;

  openAlbum(album: PhotoAlbum): void {
    this.selectedAlbum = album;
  }

  onAlbumKeydown(album: PhotoAlbum, event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.openAlbum(album);
    }
  }

  closeAlbum(): void {
    this.selectedAlbum = null;
  }

  @HostListener('document:keydown.escape')
  closeAlbumWithEscape(): void {
    if (this.selectedAlbum) {
      this.closeAlbum();
    }
  }
}

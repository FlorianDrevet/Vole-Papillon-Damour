import {CatalogNotFoundQueueTarget} from '../../../core/catalog.models';
import {toNotFoundCsv} from './not-found-export';

describe('not-found-export', () => {
  it('csv export escapes separators and quotes', () => {
    const target: CatalogNotFoundQueueTarget = {
      kind: 'edition',
      isbn13: '9782070408504',
      rareBookId: null,
      title: 'Titre; avec "guillemets"',
      authors: 'Auteur',
      publisher: null,
      publicationYear: null,
      coverUrl: null,
      genre: 'Roman',
      quantityAvailable: 2,
      reportCount: 3,
      memberCount: 2,
      firstReportedAt: '2026-09-01T10:00:00Z',
      lastReportedAt: '2026-09-05T10:00:00Z',
      overdue: false,
      comments: [],
    };

    const csv = toNotFoundCsv([target]);

    expect(csv).toContain('"Titre; avec ""guillemets"""');
    expect(csv.split('\r\n')[0]).toContain('Trouvé ?');
    expect(csv).toContain('9782070408504');
  });
});

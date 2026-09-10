import { SITE_NAV_ITEMS, getBreadcrumb } from './nav-items';

describe('site navigation labels', () => {
  it('should name the association media section photos and video library', () => {
    const associationSection = SITE_NAV_ITEMS.find(item => item.url === '/association');
    const mediaItem = associationSection?.children?.find(item => item.url === '/association/photos');

    expect(mediaItem?.label).toBe('Photos et vidéothèque');
    expect(getBreadcrumb('/association/photos')).toBe("L'association · photos et vidéothèque");
  });
});

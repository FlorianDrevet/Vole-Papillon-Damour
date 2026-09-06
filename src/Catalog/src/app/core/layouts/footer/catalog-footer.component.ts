import {ChangeDetectionStrategy, Component} from '@angular/core';

@Component({
  selector: 'app-catalog-footer',
  standalone: false,
  templateUrl: './catalog-footer.component.html',
  styleUrls: ['./catalog-footer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CatalogFooterComponent {}

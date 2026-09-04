import {ChangeDetectionStrategy, Component, OnInit} from '@angular/core';
import {ActivatedRoute} from '@angular/router';

@Component({
  selector: 'app-catalog-legal-page',
  standalone: false,
  templateUrl: './legal-page.component.html',
  styleUrls: ['./legal-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LegalPageComponent implements OnInit {
  page: 'legal' | 'privacy' = 'legal';

  constructor(private readonly route: ActivatedRoute) {}

  ngOnInit(): void {
    this.page = this.route.snapshot.data['page'] === 'privacy' ? 'privacy' : 'legal';
  }
}

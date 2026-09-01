import {Component, OnInit, signal} from '@angular/core';
import {ActualityFacadeService} from "../../shared/facades/actuality.facade.service";
import {ActivatedRoute} from "@angular/router";
import {ActualityModel} from "../../shared/models/actuality.model";

@Component({
    selector: 'app-actuality-detail',
    templateUrl: './actuality-detail.component.html',
    standalone: false
})
export class ActualityDetailComponent implements OnInit{

  actuality = signal<ActualityModel | null>(null)
  isLoading = signal(true);

  constructor(private actualityFacade: ActualityFacadeService,
              private route: ActivatedRoute) {
  }

  ngOnInit(): void {
    this.getActuality()
  }

  getActuality() {
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (id !== null) {
        this.actualityFacade.getActualityById(id)
          .then(response => this.actuality.set(response))
          .catch(() => this.actuality.set(null))
          .finally(() => this.isLoading.set(false));
      } else {
        this.isLoading.set(false);
      }
    })
  }
}

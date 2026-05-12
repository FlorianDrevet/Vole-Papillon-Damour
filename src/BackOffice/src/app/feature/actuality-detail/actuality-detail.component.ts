import {Component, OnInit, signal} from '@angular/core';
import {ActualityFacadeService} from "../../shared/facades/actuality.facade.service";
import {ActivatedRoute} from "@angular/router";
import {ActualityModel} from "../../shared/models/actuality.model";

@Component({
    selector: 'app-actuality-detail',
    templateUrl: './actuality-detail.component.html',
    styleUrl: './actuality-detail.component.scss',
    standalone: false
})
export class ActualityDetailComponent implements OnInit {

    actuality = signal<ActualityModel | null>(null)

    constructor(private actualityFacade: ActualityFacadeService,
                private route: ActivatedRoute) {
    }

    ngOnInit(): void {
        this.getActuality()
    }

    getActuality() {
        this.route.paramMap.subscribe(params => {
            if (params.get('id') !== null) {
              this.actualityFacade.getActualityById$(params.get('id')!).then(response => {
                    console.log(response)
                    this.actuality.set(response)
                })
            }
        })
    }
}

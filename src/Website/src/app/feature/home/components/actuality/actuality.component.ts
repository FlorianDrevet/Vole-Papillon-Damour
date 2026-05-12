import {Component, OnInit, signal} from '@angular/core';
import {ActualityModel} from "../../../../shared/models/actuality.model";
import {AxiosService} from "../../../../shared/services/axios.service";
import {MethodEnum} from "../../../../shared/enums/method.enum";

@Component({
    selector: 'app-actuality',
    templateUrl: './actuality.component.html',
    styleUrl: './actuality.component.scss',
    standalone: false
})
export class ActualityComponent implements OnInit{

  actualities = signal<ActualityModel[]>([])

  constructor(private axiosService: AxiosService) {
  }

  ngOnInit(): void {
    this.axiosService.request(MethodEnum.GET, "/actuality/latest", {}).then(a => {
      this.actualities.set(a);
    })
  }
}

import {inject, Injectable} from '@angular/core';
import {AxiosService} from "../services/axios.service";
import {MethodEnum} from "../enums/method.enum";
import {BingoCardInterface} from "../interfaces/bingoCard.interface";

@Injectable({
  providedIn: 'root'
})
export class BingoCardFacadeService {

  axiosService = inject(AxiosService);

  public postBingoCardAnalyze$(form: FormData): Promise<BingoCardInterface[]> {
    return this.axiosService.request$(MethodEnum.POST, `/bingo-card`, form, {}, true);
  }
}

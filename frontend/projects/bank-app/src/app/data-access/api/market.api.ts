import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { Conversion, ConversionRequest, ExchangeRates } from '../models/market.models';
import { BaseApi } from './base-api';

/** Market information shown next to the customer's money. */
@Injectable({ providedIn: 'root' })
export class MarketApi extends BaseApi {
  protected readonly root = '/api/market';

  exchangeRates(): Observable<ExchangeRates> {
    return this.http.get<ExchangeRates>(this.url('/exchange-rates'));
  }

  /** Indicative; the bank's own calculator answers when it is set up, otherwise today's rates. */
  convert(request: ConversionRequest): Observable<Conversion> {
    return this.http.post<Conversion>(this.url('/convert'), request);
  }
}

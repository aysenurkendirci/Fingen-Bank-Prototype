import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { ShareAsked, Split, SplitRequestBody } from '../models/split.models';
import { BaseApi } from './base-api';

/** Expenses split with friends, and the shares others ask of the customer. */
@Injectable({ providedIn: 'root' })
export class SplitsApi extends BaseApi {
  protected readonly root = '/api/splits';

  list(): Observable<Split[]> {
    return this.http.get<Split[]>(this.url());
  }

  create(split: SplitRequestBody): Observable<Split> {
    return this.http.post<Split>(this.url(), split);
  }

  /** Withdraws what is still open; paid shares stay paid. */
  cancel(id: number): Observable<void> {
    return this.http.delete<void>(this.url(`/${id}`));
  }

  /** The payer got this share outside the app, e.g. in cash. */
  markPaid(id: number, shareId: number): Observable<Split> {
    return this.http.post<Split>(this.url(`/${id}/shares/${shareId}/paid`), null);
  }

  requests(): Observable<ShareAsked[]> {
    return this.http.get<ShareAsked[]>(this.url('/requests'));
  }

  /** Closes a share with a transfer already made to the payer for exactly the share. */
  settle(shareId: number, transferId: number): Observable<ShareAsked> {
    return this.http.post<ShareAsked>(this.url(`/requests/${shareId}/settle`), { transferId });
  }

  decline(shareId: number): Observable<ShareAsked> {
    return this.http.post<ShareAsked>(this.url(`/requests/${shareId}/decline`), null);
  }
}

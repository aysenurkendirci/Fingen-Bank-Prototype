import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { NotificationFeed } from '../models/notification.models';
import { BaseApi } from './base-api';

/** The notification centre: what happened to the customer's money and sign-in. */
@Injectable({ providedIn: 'root' })
export class NotificationsApi extends BaseApi {
  protected readonly root = '/api/notifications';

  /** The newest notifications (at most 50) and the unread count. */
  feed(take = 20): Observable<NotificationFeed> {
    return this.http.get<NotificationFeed>(this.url(), { params: { take } });
  }

  markRead(id: number): Observable<void> {
    return this.http.post<void>(this.url(`/${id}/read`), null);
  }

  markAllRead(): Observable<void> {
    return this.http.post<void>(this.url('/read-all'), null);
  }
}

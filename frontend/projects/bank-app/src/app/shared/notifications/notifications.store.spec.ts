import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { DestroyRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { NotificationsStore } from './notifications.store';

describe('NotificationsStore', () => {
  let backend: HttpTestingController;
  let store: NotificationsStore;
  let destroy: () => void;
  let frame: DestroyRef;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])] });
    backend = TestBed.inject(HttpTestingController);
    store = TestBed.inject(NotificationsStore);

    const callbacks: (() => void)[] = [];
    destroy = () => callbacks.forEach((c) => c());
    frame = {
      onDestroy: (c: () => void) => {
        callbacks.push(c);
        return () => undefined;
      },
      destroyed: false,
    } as unknown as DestroyRef;
  });

  afterEach(() => destroy());

  it('reads the unread count as soon as the signed-in frame opens', () => {
    store.watch(frame);
    backend.expectOne((r) => r.url === '/api/notifications').flush({ items: [], unreadCount: 3 });

    expect(store.unread()).toBe(3);
  });

  it('keeps the last count when a check fails', () => {
    store.watch(frame);
    backend.expectOne((r) => r.url === '/api/notifications').flush({ items: [], unreadCount: 2 });

    store.refresh();
    backend.expectOne((r) => r.url === '/api/notifications').flush(null, { status: 503, statusText: 'Unavailable' });

    expect(store.unread()).toBe(2);
  });

  it('forgets the count on sign-out and never goes below zero', () => {
    store.watch(frame);
    backend.expectOne((r) => r.url === '/api/notifications').flush({ items: [], unreadCount: 1 });

    store.setUnread(-1);
    expect(store.unread()).toBe(0);

    store.setUnread(4);
    destroy();
    expect(store.unread()).toBe(0);
  });
});

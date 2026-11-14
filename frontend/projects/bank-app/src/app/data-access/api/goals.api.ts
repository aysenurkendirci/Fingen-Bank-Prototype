import { HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { AutoSaveRequest, Goal, GoalMoneyRequest, GoalRequest } from '../models/savings.models';
import { BaseApi } from './base-api';

/** Savings goals and the money moved in and out of them. */
@Injectable({ providedIn: 'root' })
export class GoalsApi extends BaseApi {
  protected readonly root = '/api/goals';

  list(): Observable<Goal[]> {
    return this.http.get<Goal[]>(this.url());
  }

  create(goal: GoalRequest): Observable<Goal> {
    return this.http.post<Goal>(this.url(), goal);
  }

  update(id: number, goal: GoalRequest): Observable<Goal> {
    return this.http.put<Goal>(this.url(`/${id}`), goal);
  }

  /** The same key on a retry moves the money once. */
  deposit(id: number, money: GoalMoneyRequest, idempotencyKey: string): Observable<Goal> {
    return this.http.post<Goal>(this.url(`/${id}/deposits`), money, { headers: this.key(idempotencyKey) });
  }

  withdraw(id: number, money: GoalMoneyRequest, idempotencyKey: string): Observable<Goal> {
    return this.http.post<Goal>(this.url(`/${id}/withdrawals`), money, { headers: this.key(idempotencyKey) });
  }

  setAutoSave(id: number, rule: AutoSaveRequest): Observable<Goal> {
    return this.http.put<Goal>(this.url(`/${id}/auto-save`), rule);
  }

  clearAutoSave(id: number): Observable<Goal> {
    return this.http.delete<Goal>(this.url(`/${id}/auto-save`));
  }

  close(id: number): Observable<void> {
    return this.http.delete<void>(this.url(`/${id}`));
  }

  private key(idempotencyKey: string): HttpHeaders {
    return new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
  }
}

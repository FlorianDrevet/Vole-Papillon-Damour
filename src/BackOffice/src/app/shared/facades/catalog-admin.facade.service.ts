import {Injectable} from '@angular/core';

import {MethodEnum} from '../enums/method.enum';
import {AxiosService} from '../services/axios.service';
import {
  AdminAlertOperation,
  AdminAlertPage,
  AdminAccount,
  AdminAccountFilters,
  AdminAccountPage,
  AdminAccountRole,
  CreateAdminAccountRequest,
  AdminBook,
  AdminBookFilters,
  AdminBookOperation,
  AdminBookPage,
  AdminFairPage,
  AdminFairStats,
  AdminMemberDetail,
  AdminMemberOperation,
  AdminMemberPage,
  AdminOperation,
  AdminQuantityCorrection,
  AdminScanSession,
  AdminScanSessionPage,
  AdminSessionFilters,
  AdminSettings,
  CatalogAdminOverview,
} from '../models/catalog-admin.model';

@Injectable({providedIn: 'root'})
export class CatalogAdminFacadeService {
  constructor(private readonly axiosService: AxiosService) {}

  getOverview(from?: string, to?: string): Promise<CatalogAdminOverview> {
    return this.get('/books/admin/overview', {from, to});
  }

  getAccounts(filters: AdminAccountFilters = {}): Promise<AdminAccountPage> {
    return this.get('/accounts/admin', filters);
  }

  createAccount(request: CreateAdminAccountRequest): Promise<AdminAccount> {
    return this.request(MethodEnum.POST, '/accounts/admin', request);
  }

  updateAccountRoles(externalId: string, roles: AdminAccountRole[]): Promise<AdminAccount> {
    return this.request(
      MethodEnum.PUT,
      `/accounts/admin/${encodeURIComponent(externalId)}/roles`,
      {roles},
    );
  }

  getBooks(filters: AdminBookFilters = {}): Promise<AdminBookPage> {
    return this.get('/books/admin/books', filters);
  }

  getBook(isbn13: string): Promise<AdminBook> {
    return this.get(`/books/admin/books/${encodeURIComponent(isbn13)}`);
  }

  addBook(request: object): Promise<AdminBookOperation> {
    return this.request(MethodEnum.POST, '/books/admin/books', request);
  }

  updateMetadata(isbn13: string, request: object): Promise<object> {
    return this.request(MethodEnum.PATCH, `/books/admin/books/${encodeURIComponent(isbn13)}/metadata`, request);
  }

  correctQuantity(isbn13: string, request: object): Promise<AdminQuantityCorrection> {
    return this.request(MethodEnum.PATCH, `/books/admin/books/${encodeURIComponent(isbn13)}/quantity`, request);
  }

  withdraw(isbn13: string, request: object): Promise<AdminBookOperation> {
    return this.request(MethodEnum.POST, `/books/admin/books/${encodeURIComponent(isbn13)}/withdrawals`, request);
  }

  correctAnnouncement(announcementId: string, request: object): Promise<AdminBookOperation> {
    return this.request(MethodEnum.PATCH, `/books/admin/announcements/${announcementId}/quantity`, request);
  }

  setRare(isbn13: string, isRare: boolean): Promise<object> {
    return this.request(MethodEnum.POST, `/books/admin/books/${encodeURIComponent(isbn13)}/rare?isRare=${isRare}`);
  }

  setVisibility(isbn13: string, hidden: boolean): Promise<object> {
    return this.request(MethodEnum.POST, `/books/admin/books/${encodeURIComponent(isbn13)}/visibility?hidden=${hidden}`);
  }

  merge(sourceIsbn13: string, request: object): Promise<AdminBookOperation> {
    return this.request(MethodEnum.POST, `/books/admin/books/${encodeURIComponent(sourceIsbn13)}/merge`, request);
  }

  deleteBook(isbn13: string): Promise<void> {
    return this.request(MethodEnum.DELETE, `/books/admin/books/${encodeURIComponent(isbn13)}`);
  }

  getFairs(includeCancelled = false, page = 1, pageSize = 50): Promise<AdminFairPage> {
    return this.get('/books/admin/fairs', {includeCancelled, page, pageSize});
  }

  getFairStats(fairId: string): Promise<AdminFairStats> {
    return this.get(`/books/admin/fairs/${fairId}/stats`);
  }

  setFairRevenue(fairId: string, revenue: number | null): Promise<object> {
    return this.request(MethodEnum.PUT, `/books/admin/fairs/${fairId}/revenue`, {revenue});
  }

  getSessions(filters: AdminSessionFilters = {}): Promise<AdminScanSessionPage> {
    return this.get('/books/admin/sessions', filters);
  }

  getSession(sessionId: string): Promise<AdminScanSession> {
    return this.get(`/books/admin/sessions/${sessionId}`);
  }

  removeMovement(sessionId: string, movementId: string): Promise<AdminOperation> {
    return this.request(MethodEnum.POST, `/books/admin/sessions/${sessionId}/movements/${movementId}/remove`);
  }

  reassignSession(sessionId: string, request: object): Promise<AdminOperation> {
    return this.request(MethodEnum.POST, `/books/admin/sessions/${sessionId}/reassign`, request);
  }

  cancelSession(sessionId: string): Promise<AdminOperation> {
    return this.request(MethodEnum.POST, `/books/admin/sessions/${sessionId}/cancel`);
  }

  cancelSessionAlerts(sessionId: string): Promise<AdminOperation> {
    return this.request(MethodEnum.POST, `/books/admin/sessions/${sessionId}/alerts/cancel`);
  }

  forceSessionAlerts(sessionId: string): Promise<AdminOperation> {
    return this.request(MethodEnum.POST, `/books/admin/sessions/${sessionId}/alerts/force`);
  }

  getAlerts(filters: object = {}): Promise<AdminAlertPage> {
    return this.get('/books/admin/alerts', filters);
  }

  cancelAlert(messageId: string): Promise<AdminAlertOperation> {
    return this.request(MethodEnum.POST, `/books/admin/alerts/${messageId}/cancel`);
  }

  forceAlert(messageId: string): Promise<AdminAlertOperation> {
    return this.request(MethodEnum.POST, `/books/admin/alerts/${messageId}/force`);
  }

  getMembers(search?: string, alertStatus?: string, page = 1, pageSize = 50): Promise<AdminMemberPage> {
    return this.get('/books/admin/members', {search, alertStatus, page, pageSize});
  }

  getMember(memberId: string): Promise<AdminMemberDetail> {
    return this.get(`/books/admin/members/${memberId}`);
  }

  blockMember(memberId: string): Promise<AdminMemberOperation> {
    return this.request(MethodEnum.POST, `/books/admin/members/${memberId}/block`);
  }

  unblockMember(memberId: string): Promise<AdminMemberOperation> {
    return this.request(MethodEnum.POST, `/books/admin/members/${memberId}/unblock`);
  }

  deleteMember(memberId: string): Promise<AdminMemberOperation> {
    return this.request(MethodEnum.DELETE, `/books/admin/members/${memberId}`);
  }

  getSettings(): Promise<AdminSettings> {
    return this.get('/books/admin/settings');
  }

  updateSettings(request: object): Promise<AdminSettings> {
    return this.request(MethodEnum.PUT, '/books/admin/settings', request);
  }

  private get<T>(url: string, params?: object): Promise<T> {
    return this.axiosService.request$(MethodEnum.GET, url, params ?? null) as Promise<T>;
  }

  private request<T>(method: MethodEnum, url: string, body?: object): Promise<T> {
    return this.axiosService.request$(method, url, body ?? null) as Promise<T>;
  }
}

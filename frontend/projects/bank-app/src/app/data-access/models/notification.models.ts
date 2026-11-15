export type NotificationKind = 'money-in' | 'money-out' | 'savings' | 'goal' | 'security';

export interface AppNotification {
  notificationId: number;
  kind: NotificationKind;
  title: string;
  body: string;
  createdAt: string;
  /** Null while unread. */
  readAt: string | null;
}

export interface NotificationFeed {
  items: AppNotification[];
  unreadCount: number;
}

// Request and response shapes of the sign-in endpoints; they mirror Bank.Contracts.Auth.
export interface LoginRequest {
  tcNo: string;
  password: string;
  /** The authenticator app code, sent only once the API asked for it. */
  code?: string;
}

export interface RegisterRequest {
  tcNo: string;
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  password: string;
  membership: 'Corporate' | 'Personal';
}

export interface AuthResponse {
  userId: number;
  fullName: string;
  token: string;
}


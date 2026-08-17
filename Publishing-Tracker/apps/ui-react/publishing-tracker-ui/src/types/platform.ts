export interface Platform {
  id: number;
  name: string;
  baseUrl: string;
  commissionRate: number;
  isActive: boolean;
}

export interface PlatformRequest {
  name: string;
  baseUrl: string;
  commissionRate: number;
}
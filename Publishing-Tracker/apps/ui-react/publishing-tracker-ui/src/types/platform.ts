export interface Platform {
  id: number;
  name: string;
  baseUrl: string;
  commissionRate: number;
}

export interface PlatformRequest {
  name: string;
  baseUrl: string;
  commissionRate: number;
}
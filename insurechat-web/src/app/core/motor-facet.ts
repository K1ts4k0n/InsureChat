import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import {
  FALLBACK_BRANDS,
  FALLBACK_YEARS,
  FALLBACK_PROVINCES,
  getFallbackModels,
  getFallbackPackages,
  getFallbackPackageDetail
} from './motor-fallback-data';

export interface FacetItem { value: string; label: string; }

/** ความคุ้มครองเต็มของแพคเกจ 1 รายการ (modal "ดูรายละเอียด") — ตรงกับ MotorPackageDetail ฝั่ง api */
export interface MotorPackageDetail {
  packageId: number;
  insurerName: string; insurerLogo: string; packageName: string; polTypeName: string;
  carUsage: string; renovType: string;
  sumInsure: number; deductible: number; premNet: number; premTotal: number;
  fireTheft: number; flood: number; terrorism: number;
  thirdBodilyPers: number; thirdBodilyAccid: number; thirdProperty: number; thirdPropertyDeduc: number;
  bodilyDrivCount: number; bodilyDrivCover: number; bodilyPassCount: number; bodilyPassCover: number;
  medicalPers: number; medicalBaht: number; bailBondIns: number;
}
export interface PolType { polTypeId: number; polTypeName: string; }
export interface PackageRow {
  packageId: number;
  insurerName: string;
  insurerLogo: string;     // URL (อาจว่างถ้าไม่มี LogoPath)
  packageName: string;
  polTypeName: string;
  renovType: string;       // อู่ | ห้าง
  sumInsureMax: number;
  premNet: number;         // เบี้ยสุทธิ/ปี
  premStm: number;         // เบี้ยรวมหลังหักส่วนลด/ปี
  premTotal: number;       // เบี้ยรวมอากรดิบ
  discount: number;
  discType: string;        // % | บาท | อื่นๆ | ''
  fireSuminsure: number;
  floodSuminsure: number;
}
export interface PackageSearchRequest {
  brand: string; model: string; cc: string; year: number;
  provinId?: number | null;
  polTypeId?: number | null; sumInsure?: number | null;
  fireSum?: number | null; floodSum?: number | null; sort?: string;
}
export interface ResolvedVehicle {
  brand: string | null; model: string | null; year: number | null; cc: string;
  brandOk: boolean; modelOk: boolean; yearOk: boolean;
}
export interface DocumentExtractResponse {
  isVehicleDocument: boolean; docType: string; table: string;
  resolved: ResolvedVehicle; unresolved: string[]; message: string;
}

/** ผล resolve ค่าที่ผู้ใช้พิมพ์ → ค่า facet canonical (ADR 0019) */
export interface VehicleResolveResult {
  brand: string | null; model: string | null; year: number | null;
  brandOk: boolean; modelOk: boolean; yearOk: boolean;
}

@Injectable({ providedIn: 'root' })
export class MotorFacet {
  private base = `${environment.apiUrl}/Motor`;
  readonly isOfflineMode = signal(false);

  constructor(private http: HttpClient) {}

  brands(): Observable<FacetItem[]> {
    return this.http.get<FacetItem[]>(`${this.base}/Brands`).pipe(
      catchError(() => {
        this.isOfflineMode.set(true);
        return of(FALLBACK_BRANDS);
      })
    );
  }

  models(brand: string): Observable<FacetItem[]> {
    return this.http.get<FacetItem[]>(`${this.base}/Models`, { params: { brand } }).pipe(
      catchError(() => of(getFallbackModels(brand)))
    );
  }

  cc(brand: string, model: string): Observable<FacetItem[]> {
    return this.http.get<FacetItem[]>(`${this.base}/Cc`, { params: { brand, model } }).pipe(
      catchError(() => of([]))
    );
  }

  years(brand: string, model: string, cc: string): Observable<FacetItem[]> {
    return this.http.get<FacetItem[]>(`${this.base}/Years`, { params: { brand, model, cc } }).pipe(
      catchError(() => of(FALLBACK_YEARS))
    );
  }

  polTypes(brand: string, model: string, cc: string, year: number): Observable<PolType[]> {
    return this.http.get<PolType[]>(`${this.base}/PolTypes`, { params: { brand, model, cc, year } }).pipe(
      catchError(() => of([]))
    );
  }

  sumInsures(brand: string, model: string, cc: string, year: number, polTypeId?: number | null): Observable<FacetItem[]> {
    return this.http.get<FacetItem[]>(`${this.base}/SumInsures`, { params: this.facetParams(brand, model, cc, year, polTypeId) }).pipe(
      catchError(() => of([]))
    );
  }

  fireSumInsures(brand: string, model: string, cc: string, year: number, polTypeId?: number | null): Observable<FacetItem[]> {
    return this.http.get<FacetItem[]>(`${this.base}/FireSumInsures`, { params: this.facetParams(brand, model, cc, year, polTypeId) }).pipe(
      catchError(() => of([]))
    );
  }

  floodSumInsures(brand: string, model: string, cc: string, year: number, polTypeId?: number | null): Observable<FacetItem[]> {
    return this.http.get<FacetItem[]>(`${this.base}/FloodSumInsures`, { params: this.facetParams(brand, model, cc, year, polTypeId) }).pipe(
      catchError(() => of([]))
    );
  }

  provinces(): Observable<FacetItem[]> {
    return this.http.get<FacetItem[]>(`${this.base}/Provinces`).pipe(
      catchError(() => of(FALLBACK_PROVINCES))
    );
  }

  search(req: PackageSearchRequest): Observable<PackageRow[]> {
    return this.http.post<PackageRow[]>(`${this.base}/Search`, req).pipe(
      catchError(() => of(getFallbackPackages(req.brand, req.model)))
    );
  }

  packageDetail(packageId: number): Observable<MotorPackageDetail> {
    return this.http.get<MotorPackageDetail>(`${this.base}/PackageDetail`, { params: { packageId } }).pipe(
      catchError(() => of(getFallbackPackageDetail(packageId)))
    );
  }

  /** แนบเอกสารรถ (Blob รูป/PDF) → vision สกัด → resolved ค่า facet (ADR 0018) */
  extractDocument(file: Blob, filename = 'document'): Observable<DocumentExtractResponse> {
    const form = new FormData();
    form.append('file', file, filename);
    return this.http.post<DocumentExtractResponse>(`${this.base}/ExtractDocument`, form);
  }

  /** resolve ค่าที่ผู้ใช้พิมพ์เอง → ค่า facet canonical (intake gate, ADR 0019) */
  resolveVehicle(brand: string, model?: string, year?: number): Observable<VehicleResolveResult> {
    const params: Record<string, string> = { brand };
    if (model) params['model'] = model;
    if (year != null) params['year'] = String(year);
    return this.http.get<VehicleResolveResult>(`${this.base}/ResolveVehicle`, { params });
  }

  private facetParams(brand: string, model: string, cc: string, year: number, polTypeId?: number | null): any {
    const params: any = { brand, model, cc, year };
    if (polTypeId != null) params.polTypeId = polTypeId;
    return params;
  }
}

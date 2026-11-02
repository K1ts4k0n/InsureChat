export const ACCEPTED_DOC_TYPES = ['image/png', 'image/jpeg', 'image/webp', 'application/pdf'];
export const MAX_DOC_BYTES = 8 * 1024 * 1024;

/** null = ผ่าน, string = ข้อความ error ภาษาไทย */
export function validateDocFile(file: File): string | null {
  if (!ACCEPTED_DOC_TYPES.includes(file.type)) return 'รองรับเฉพาะรูป JPG/PNG/WebP หรือ PDF';
  if (file.size > MAX_DOC_BYTES) return 'ไฟล์ใหญ่เกิน 8MB';
  return null;
}

/** ย่อรูป (ไม่ใช่ PDF) ให้ด้านยาวสุด ≤ max px เพื่อประหยัด token; คืนไฟล์เดิมถ้าไม่ต้องย่อ/ล้มเหลว */
export async function downscaleImage(file: File, max = 2000): Promise<Blob> {
  if (file.type === 'application/pdf') return file;
  try {
    const bmp = await createImageBitmap(file);
    if (Math.max(bmp.width, bmp.height) <= max) { bmp.close(); return file; }
    const scale = max / Math.max(bmp.width, bmp.height);
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(bmp.width * scale);
    canvas.height = Math.round(bmp.height * scale);
    canvas.getContext('2d')!.drawImage(bmp, 0, 0, canvas.width, canvas.height);
    bmp.close();
    return await new Promise<Blob>((res) => canvas.toBlob((b) => res(b ?? file), 'image/jpeg', 0.9));
  } catch {
    return file;
  }
}

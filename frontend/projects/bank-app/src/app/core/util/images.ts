/** The longest side a photo keeps; enough for a card on any screen. */
const MAX_SIDE = 1600;
const QUALITY = 0.85;

/**
 * Redraws a picture the customer picked as a JPEG of at most 1600 pixels a side. Phones take photos of several
 * megabytes, and their EXIF data can hold where the photo was taken; drawing it again keeps only the pixels.
 * Throws when the file is not an image the browser can read.
 */
export async function shrinkImage(file: Blob): Promise<Blob> {
  const bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
  try {
    const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height));
    const width = Math.round(bitmap.width * scale);
    const height = Math.round(bitmap.height * scale);

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext('2d');
    if (!context) throw new Error('Canvas is not available.');
    // Transparent PNGs become white rather than black in a JPEG.
    context.fillStyle = '#fff';
    context.fillRect(0, 0, width, height);
    context.drawImage(bitmap, 0, 0, width, height);

    return await new Promise<Blob>((resolve, reject) =>
      canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('The image could not be encoded.'))), 'image/jpeg', QUALITY),
    );
  } finally {
    bitmap.close();
  }
}

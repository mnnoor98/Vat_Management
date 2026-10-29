import { Injectable } from '@angular/core';

declare const html2pdf: any;

@Injectable({
  providedIn: 'root'
})
export class PdfExportService {

  public downloadElementAsPdf(
    elementId: string,
    filename: string,
    orientation: 'portrait' | 'landscape' = 'portrait'
  ): void {
    const element = document.getElementById(elementId);
    if (!element) {
      console.warn(`Element with ID '${elementId}' not found for PDF export.`);
      window.print();
      return;
    }

    const cleanFilename = filename.endsWith('.pdf') ? filename : `${filename}.pdf`;

    if (typeof html2pdf !== 'undefined') {
      const opt = {
        margin: [10, 12, 12, 12],
        filename: cleanFilename,
        image: { type: 'jpeg', quality: 0.98 },
        html2canvas: {
          scale: 2,
          useCORS: true,
          logging: false
        },
        jsPDF: {
          unit: 'mm',
          format: 'a4',
          orientation: orientation
        }
      };

      html2pdf().set(opt).from(element).save().catch((err: any) => {
        console.error('Error generating PDF via html2pdf:', err);
        window.print();
      });
    } else {
      // Fallback to native print dialog with clean @media print styles
      window.print();
    }
  }

  public printClean(): void {
    window.print();
  }
}

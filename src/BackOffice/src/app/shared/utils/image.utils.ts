import {WritableSignal} from "@angular/core";

export class ImageUtils {
  static createBlobFromImage(image: string) {
    const base64Data = image.split(',')[1]; // Supprimez "data:image/png;base64," du début
    const binaryData = atob(base64Data);
    const byteArray = new Uint8Array(binaryData.length);
    for (let i = 0; i < binaryData.length; i++) {
      byteArray[i] = binaryData.charCodeAt(i);
    }
    return new Blob([byteArray], {type: "image/*"});
  }

  static onFileSelected(inputNode: any,  upload: WritableSignal<any>) {
    if (typeof (FileReader) !== 'undefined') {
      const reader = new FileReader();

      reader.onload = (e: any) => {
        upload.set({fileName: inputNode.files[0].name, fileContent: e.target!.result});
      };

      reader.readAsDataURL(inputNode.files[0]);
    }
  }


  static onFilesSelected(inputNode: any, uploadImage: WritableSignal<any[]>) {

    if (typeof (FileReader) !== 'undefined') {
      const files = inputNode.files;
      for (let i = 0; i < files.length; i++) {
        const reader = new FileReader();
        reader.onload = (e: any) => {
          uploadImage.update(x => [...x, {fileName: files[i].name, fileContent: e.target!.result}]);
        };
        reader.readAsDataURL(files[i]);
      }
    }
  }
}

// Déclenche un téléchargement (data:URL base64) côté navigateur
window.saveFile = (fileName, contentType, base64Data) => {
  try {
    const link = document.createElement('a');
    link.download = fileName || 'download';
    link.href = "data:" + (contentType || "application/octet-stream") + ";base64," + (base64Data || "");
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  } catch (e) {
    console.error("saveFile error:", e);
    alert("Impossible de déclencher le téléchargement.");
  }
};

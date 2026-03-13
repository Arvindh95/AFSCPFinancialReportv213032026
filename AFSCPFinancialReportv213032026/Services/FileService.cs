using System;
using System.Linq;
using FinancialReport.Helper;
using PX.Data;
using PX.SM;

namespace FinancialReport.Services
{
    public class FileService
    {
        private readonly PXGraph _graph;

        public FileService(PXGraph graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        /// <summary>
        /// Retrieves file content from Acumatica using NoteID.
        /// </summary>
        public (byte[] fileBytes, string fileName) GetFileContentAndName(Guid? noteID, FLRTFinancialReport currentRecord)
        {
            if (noteID == null)
            {
                PXTrace.WriteError("NoteID is null.");
                throw new PXException(Messages.NoteIDIsNull);
            }

            PXTrace.WriteInformation($"Fetching files for NoteID = {noteID}");

            // Uses the constant for the template filter string
            var uploadedFiles = new PXSelectJoin<UploadFile,
                InnerJoin<NoteDoc, On<UploadFile.fileID, Equal<NoteDoc.fileID>>>,
                Where<
                    NoteDoc.noteID, Equal<Required<NoteDoc.noteID>>,
                    And<UploadFile.name, Like<Required<UploadFile.name>>>>,
                OrderBy<Desc<UploadFile.createdDateTime>>>(_graph)
                .Select(noteID, $"%{Constants.TemplateFileFilter}%");

            PXTrace.WriteInformation($"Files found for NoteID = {noteID}: {uploadedFiles?.Count ?? 0}");

            if (uploadedFiles == null || uploadedFiles.Count == 0)
            {
                PXTrace.WriteError("No files associated with this NoteID.");
                throw new PXException(Messages.NoFilesAssociated);
            }

            foreach (PXResult<UploadFile, NoteDoc> result in uploadedFiles)
            {
                var file = (UploadFile)result;
                PXTrace.WriteInformation($"Checking file: {file.Name}, FileID: {file.FileID}");

                UploadFileRevision fileRevision = PXSelect<UploadFileRevision,
                    Where<UploadFileRevision.fileID, Equal<Required<UploadFileRevision.fileID>>>,
                    OrderBy<Desc<UploadFileRevision.createdDateTime>>>
                    .Select(_graph, file.FileID)
                    .RowCast<UploadFileRevision>()
                    .FirstOrDefault();

                if (fileRevision == null)
                {
                    PXTrace.WriteWarning($"No revision found for FileID: {file.FileID}");
                    continue;
                }

                if (fileRevision.BlobData != null && fileRevision.BlobData.Length > 0)
                {
                    PXTrace.WriteInformation($"Found valid file revision. Size: {fileRevision.BlobData.Length} bytes");
                    currentRecord.UploadedFileID = file.FileID;
                    return (fileRevision.BlobData, file.Name);
                }
                else
                {
                    PXTrace.WriteError($"File revision has no BlobData for FileID: {file.FileID}");
                }
            }

            PXTrace.WriteError("Failed to retrieve any usable file revision.");
            throw new PXException(Messages.FailedToRetrieveFile);
        }

        /// <summary>
        /// Saves a generated document into Acumatica and returns the FileID.
        /// </summary>
        public Guid SaveGeneratedDocument(string fileName, byte[] fileContent, FLRTFinancialReport currentRecord)
        {
            if (fileContent == null || fileContent.Length == 0)
                throw new PXException(Messages.TemplateFileIsEmpty);

            var fileGraph = PXGraph.CreateInstance<UploadFileMaintenance>();

            var fileInfo = new PX.SM.FileInfo(fileName, null, fileContent)
            {
                IsPublic = true
            };

            bool saved = fileGraph.SaveFile(fileInfo);
            if (!saved)
            {
                throw new PXException(Messages.UnableToSaveGeneratedFile);
            }

            if (fileInfo.UID.HasValue)
            {
                PXNoteAttribute.SetFileNotes(
                    _graph.Caches[typeof(FLRTFinancialReport)],
                    currentRecord,
                    fileInfo.UID.Value
                );

                var existingLink = PXSelect<NoteDoc,
                    Where<NoteDoc.noteID, Equal<Required<NoteDoc.noteID>>,
                          And<NoteDoc.fileID, Equal<Required<NoteDoc.fileID>>>>>
                    .Select(_graph, currentRecord.Noteid, fileInfo.UID.Value)
                    .FirstOrDefault();

                if (existingLink == null)
                {
                    PXDatabase.Insert<NoteDoc>(
                        new PXDataFieldAssign<NoteDoc.noteID>(currentRecord.Noteid),
                        new PXDataFieldAssign<NoteDoc.fileID>(fileInfo.UID.Value)
                    );
                }
            }

            return fileInfo.UID ?? Guid.Empty;
        }
    }
}

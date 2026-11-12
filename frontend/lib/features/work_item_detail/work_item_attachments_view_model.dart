import 'dart:typed_data';

import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/work_item_detail/data/attachment_camera.dart';
import 'package:weaver/features/work_item_detail/data/attachment_files.dart';
import 'package:weaver/features/work_item_detail/data/photo_capture_exception.dart';
import 'package:weaver/features/work_item_detail/data/work_item_attachment_repository.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';
import 'package:weaver/features/work_item_detail/models/photo_downscale.dart';
import 'package:weaver/features/work_item_detail/models/work_item_attachment.dart';

/// The detail dialog's Attachments section. Kept apart from
/// [WorkItemDetailViewModel]: attachments never change the item itself, so
/// they need none of its version or conflict handling.
@injectable
class WorkItemAttachmentsViewModel extends ViewModel {
  WorkItemAttachmentsViewModel(this._repository, this._files, this._camera);

  final WorkItemAttachmentRepository _repository;
  final AttachmentFiles _files;
  final AttachmentCamera _camera;

  /// By attachment id. The list rebuilds on every change, and a thumbnail
  /// never changes, so each is fetched once per dialog.
  final Map<String, Future<Uint8List>> _thumbnails = {};

  String? _workItemId;
  List<WorkItemAttachment> _attachments = const [];
  bool _isLoading = false;
  String? _loadError;
  bool _isUploading = false;
  String? _error;

  /// Oldest first, as the server sends them.
  List<WorkItemAttachment> get attachments => _attachments;
  bool get isLoading => _isLoading;
  String? get loadError => _loadError;
  bool get isUploading => _isUploading;

  /// Whether "Take photo" is offered: only on phones.
  bool get canTakePhoto => _camera.isAvailable;

  /// Why the last upload, download or delete failed; cleared by the next.
  String? get error => _error;

  Future<void> load(String workItemId) async {
    _workItemId = workItemId;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();

    try {
      _attachments = await _repository.loadAttachments(workItemId);
    } on ApiException {
      _attachments = const [];
      _loadError = 'Could not load the attachments.';
    } finally {
      _isLoading = false;
      notifyIfActive();
    }
  }

  /// Asks the user for a file and attaches it. False if they cancel or it
  /// fails, which [error] then explains.
  Future<bool> upload() async {
    final file = await _files.pick();
    if (file == null) return false;
    return _attach(file);
  }

  /// Opens the camera and attaches the photo taken, downscaled so it fits
  /// well within the server's size limit. False if the user cancels or it
  /// fails, which [error] then explains.
  Future<bool> takePhoto() async {
    final AttachmentUpload? photo;
    try {
      photo = await _camera.takePhoto(PhotoDownscale.forUpload);
    } on PhotoCaptureException {
      _error = 'Could not take a photo. Check that Weaver may use the camera.';
      notifyIfActive();
      return false;
    }
    if (photo == null) return false;
    return _attach(photo);
  }

  /// [attachment]'s preview, for one whose [WorkItemAttachment.hasThumbnail]
  /// is set. A failed fetch is kept too, so a missing thumbnail shows the
  /// file icon instead of being refetched on every rebuild.
  Future<Uint8List> thumbnail(WorkItemAttachment attachment) =>
      _thumbnails.putIfAbsent(
        attachment.id,
        () => _repository.downloadThumbnail(attachment.id),
      );

  /// [attachment]'s full-size content, for viewing an image. Not cached: an
  /// original can be many megabytes and is only looked at now and then.
  Future<Uint8List> imageContent(WorkItemAttachment attachment) =>
      _repository.downloadContent(attachment.id);

  Future<bool> _attach(AttachmentUpload file) async {
    _isUploading = true;
    _error = null;
    notifyIfActive();
    try {
      final created = await _repository.upload(_workItemId!, file);
      _attachments = [..._attachments, created];
      return true;
    } on ApiException catch (e) {
      _error = switch (e) {
        // The server names its size limit; a proxy in front of it may
        // refuse a large file first, without saying what the limit is.
        ApiException(statusCode: 413) =>
          e.detail ?? '${file.fileName} is too large to attach.',
        // E.g. an empty file, which the server says better than "try
        // again" can.
        ApiException(statusCode: 400) => e.message,
        _ => 'Could not upload ${file.fileName}. Try again.',
      };
      return false;
    } finally {
      _isUploading = false;
      notifyIfActive();
    }
  }

  /// Fetches [attachment]'s content (it needs the sign-in token, so a plain
  /// link can't) and gives it to the user as a file.
  Future<bool> download(WorkItemAttachment attachment) => _attempt(() async {
    final bytes = await _repository.downloadContent(attachment.id);
    await _files.save(
      attachment.fileName,
      bytes,
      contentType: attachment.contentType,
    );
  }, errorMessage: 'Could not download ${attachment.fileName}. Try again.');

  Future<bool> delete(WorkItemAttachment attachment) => _attempt(() async {
    await _repository.delete(attachment.id);
    _thumbnails.remove(attachment.id)?.ignore();
    _attachments = [
      for (final a in _attachments)
        if (a.id != attachment.id) a,
    ];
  }, errorMessage: 'Could not delete ${attachment.fileName}. Try again.');

  Future<bool> _attempt(
    Future<void> Function() action, {
    required String errorMessage,
  }) async {
    _error = null;
    notifyIfActive();
    try {
      await action();
      return true;
    } on ApiException {
      _error = errorMessage;
      return false;
    } finally {
      notifyIfActive();
    }
  }
}

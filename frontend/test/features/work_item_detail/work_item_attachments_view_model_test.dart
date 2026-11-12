import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/data/photo_capture_exception.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';
import 'package:weaver/features/work_item_detail/work_item_attachments_view_model.dart';

import 'fakes/fake_attachment_camera.dart';
import 'fakes/fake_attachment_files.dart';
import 'fakes/fake_attachment_repository.dart';

const _picked = AttachmentUpload(
  fileName: 'quote.pdf',
  bytes: [1, 2, 3],
  contentType: 'application/pdf',
);

const _photo = AttachmentUpload(
  fileName: 'Photo 2026-11-12 14.03.22.jpg',
  bytes: [0xff, 0xd8, 0xff],
  contentType: 'image/jpeg',
);

WorkItemAttachmentsViewModel _viewModel(
  FakeAttachmentRepository repository, {
  FakeAttachmentFiles? files,
  FakeAttachmentCamera? camera,
}) => WorkItemAttachmentsViewModel(
  repository,
  files ?? FakeAttachmentFiles(),
  camera ?? FakeAttachmentCamera(),
);

Future<WorkItemAttachmentsViewModel> _loaded(
  FakeAttachmentRepository repository, {
  FakeAttachmentFiles? files,
  FakeAttachmentCamera? camera,
}) async {
  final viewModel = _viewModel(repository, files: files, camera: camera);
  await viewModel.load('item-1');
  return viewModel;
}

void main() {
  group('load', () {
    test("lists the item's attachments", () async {
      final viewModel = _viewModel(
        FakeAttachmentRepository([
          attachment(),
          attachment(id: 'att-2', fileName: 'notes.txt'),
        ]),
      );

      await viewModel.load('item-1');

      expect(viewModel.isLoading, isFalse);
      expect(viewModel.loadError, isNull);
      expect(viewModel.attachments.map((a) => a.fileName), [
        'report.pdf',
        'notes.txt',
      ]);
    });

    test('says so when the list cannot be loaded', () async {
      final repository = FakeAttachmentRepository([attachment()])
        ..failure = const ApiException('down', statusCode: 500);
      final viewModel = _viewModel(repository);

      await viewModel.load('item-1');

      expect(viewModel.isLoading, isFalse);
      expect(viewModel.loadError, 'Could not load the attachments.');
      expect(viewModel.attachments, isEmpty);
    });
  });

  group('upload', () {
    test('uploads the picked file to the item and lists it last', () async {
      final repository = FakeAttachmentRepository([attachment()]);
      final viewModel = await _loaded(
        repository,
        files: FakeAttachmentFiles(picked: _picked),
      );

      final uploaded = await viewModel.upload();

      expect(uploaded, isTrue);
      expect(repository.uploads, [_picked]);
      expect(viewModel.attachments.map((a) => a.fileName), [
        'report.pdf',
        'quote.pdf',
      ]);
      expect(viewModel.isUploading, isFalse);
      expect(viewModel.error, isNull);
    });

    test('clears the last error when opened, even if the user then '
        'cancels', () async {
      final repository = FakeAttachmentRepository();
      final files = FakeAttachmentFiles(picked: _picked);
      final viewModel = await _loaded(repository, files: files);
      repository.failure = const ApiException('boom', statusCode: 500);
      await viewModel.upload();
      expect(viewModel.error, isNotNull);

      files.picked = null;
      await viewModel.upload();

      expect(viewModel.error, isNull);
    });

    test('uploads nothing when the user cancels the pick', () async {
      final repository = FakeAttachmentRepository();
      final viewModel = await _loaded(repository);

      final uploaded = await viewModel.upload();

      expect(uploaded, isFalse);
      expect(repository.uploads, isEmpty);
      expect(viewModel.error, isNull);
    });

    test("shows the server's message for a file over the limit", () async {
      final repository = FakeAttachmentRepository();
      final viewModel = await _loaded(
        repository,
        files: FakeAttachmentFiles(picked: _picked),
      );
      repository.failure = const ApiException(
        'Attachments can be at most 25 MB. (413).',
        statusCode: 413,
        detail: 'Attachments can be at most 25 MB.',
      );

      final uploaded = await viewModel.upload();

      expect(uploaded, isFalse);
      expect(viewModel.error, 'Attachments can be at most 25 MB.');
      expect(viewModel.attachments, isEmpty);
      expect(viewModel.isUploading, isFalse);
    });

    test('still says the file is too large when a 413 has no message, '
        'as when a proxy refuses it before the server sees it', () async {
      final repository = FakeAttachmentRepository();
      final viewModel = await _loaded(
        repository,
        files: FakeAttachmentFiles(picked: _picked),
      );
      repository.failure = const ApiException(
        'Failed to upload the attachment (413).',
        statusCode: 413,
      );

      await viewModel.upload();

      expect(viewModel.error, 'quote.pdf is too large to attach.');
    });

    test('names the file when the upload fails otherwise', () async {
      final repository = FakeAttachmentRepository();
      final viewModel = await _loaded(
        repository,
        files: FakeAttachmentFiles(picked: _picked),
      );
      repository.failure = const ApiException('boom', statusCode: 500);

      await viewModel.upload();

      expect(viewModel.error, 'Could not upload quote.pdf. Try again.');
    });
  });

  group('takePhoto', () {
    test('is offered only where the device has a camera to use', () {
      expect(
        _viewModel(
          FakeAttachmentRepository(),
          camera: FakeAttachmentCamera(isAvailable: true),
        ).canTakePhoto,
        isTrue,
      );
      expect(_viewModel(FakeAttachmentRepository()).canTakePhoto, isFalse);
    });

    test('uploads the photo and lists it', () async {
      final repository = FakeAttachmentRepository();
      final camera = FakeAttachmentCamera(isAvailable: true, photo: _photo);
      final viewModel = await _loaded(repository, camera: camera);

      final uploaded = await viewModel.takePhoto();

      expect(uploaded, isTrue);
      expect(camera.timesOpened, 1);
      expect(repository.uploads, [_photo]);
      expect(viewModel.attachments.map((a) => a.fileName), [_photo.fileName]);
      expect(viewModel.isUploading, isFalse);
    });

    test('uploads nothing when the user cancels the camera', () async {
      final repository = FakeAttachmentRepository();
      final viewModel = await _loaded(
        repository,
        camera: FakeAttachmentCamera(isAvailable: true),
      );

      final uploaded = await viewModel.takePhoto();

      expect(uploaded, isFalse);
      expect(repository.uploads, isEmpty);
      expect(viewModel.error, isNull);
      expect(viewModel.isUploading, isFalse);
    });

    Future<String?> errorWhenCameraFails(String code) async {
      final repository = FakeAttachmentRepository();
      final viewModel = await _loaded(
        repository,
        camera: FakeAttachmentCamera(
          isAvailable: true,
          failure: PhotoCaptureException(code),
        ),
      );

      final uploaded = await viewModel.takePhoto();

      expect(uploaded, isFalse);
      expect(repository.uploads, isEmpty);
      return viewModel.error;
    }

    test('says how to allow the camera when access is denied', () async {
      expect(
        await errorWhenCameraFails(PhotoCaptureException.accessDenied),
        'Weaver may not use the camera. Allow camera access in the '
        "device's settings to take a photo.",
      );
    });

    test('says so when the device has no camera', () async {
      expect(
        await errorWhenCameraFails(PhotoCaptureException.noCamera),
        'This device has no camera to take a photo with.',
      );
    });

    test('says to try again when the camera fails otherwise', () async {
      expect(
        await errorWhenCameraFails('already_active'),
        'Could not take a photo. Try again.',
      );
    });

    test('clears the last error when opened, even if the user then '
        'cancels', () async {
      final camera = FakeAttachmentCamera(
        isAvailable: true,
        failure: const PhotoCaptureException(PhotoCaptureException.noCamera),
      );
      final viewModel = await _loaded(
        FakeAttachmentRepository(),
        camera: camera,
      );
      await viewModel.takePhoto();
      expect(viewModel.error, isNotNull);

      camera.failure = null;
      await viewModel.takePhoto();

      expect(viewModel.error, isNull);
    });

    test("shows the server's message for a photo over the limit", () async {
      final repository = FakeAttachmentRepository();
      final viewModel = await _loaded(
        repository,
        camera: FakeAttachmentCamera(isAvailable: true, photo: _photo),
      );
      repository.failure = const ApiException(
        'Attachments can be at most 25 MB. (413).',
        statusCode: 413,
        detail: 'Attachments can be at most 25 MB.',
      );

      final uploaded = await viewModel.takePhoto();

      expect(uploaded, isFalse);
      expect(viewModel.error, 'Attachments can be at most 25 MB.');
    });
  });

  group('thumbnail', () {
    test('fetches the thumbnail once and reuses it, so rebuilding the list '
        'does not fetch it again', () async {
      final photo = attachment(hasThumbnail: true);
      final bytes = Uint8List.fromList([1, 2, 3]);
      final repository = FakeAttachmentRepository([photo])
        ..thumbnails[photo.id] = bytes;
      final viewModel = await _loaded(repository);

      expect(await viewModel.thumbnail(photo), bytes);
      expect(await viewModel.thumbnail(photo), bytes);
      expect(viewModel.thumbnail(photo), same(viewModel.thumbnail(photo)));
      expect(repository.thumbnailRequests, [photo.id]);
    });

    test('keeps a failed fetch too, rather than retrying it on every '
        'rebuild', () async {
      final photo = attachment(hasThumbnail: true);
      final repository = FakeAttachmentRepository([photo]);
      final viewModel = await _loaded(repository);

      await expectLater(
        viewModel.thumbnail(photo),
        throwsA(isA<ApiException>()),
      );
      await expectLater(
        viewModel.thumbnail(photo),
        throwsA(isA<ApiException>()),
      );

      expect(repository.thumbnailRequests, [photo.id]);
    });

    test("fetches each attachment's own thumbnail", () async {
      final first = attachment(hasThumbnail: true);
      final second = attachment(id: 'att-2', hasThumbnail: true);
      final repository = FakeAttachmentRepository([first, second])
        ..thumbnails[first.id] = Uint8List.fromList([1])
        ..thumbnails[second.id] = Uint8List.fromList([2]);
      final viewModel = await _loaded(repository);

      expect(await viewModel.thumbnail(first), [1]);
      expect(await viewModel.thumbnail(second), [2]);
    });
  });

  test('imageContent fetches the full-size original', () async {
    final photo = attachment(hasThumbnail: true);
    final original = Uint8List.fromList([9, 9, 9]);
    final repository = FakeAttachmentRepository([photo])
      ..contents[photo.id] = original;
    final viewModel = await _loaded(repository);

    expect(await viewModel.imageContent(photo), original);
  });

  group('download', () {
    test("saves the file's content under its name and type", () async {
      final report = attachment();
      final content = Uint8List.fromList([37, 80, 68, 70]);
      final repository = FakeAttachmentRepository([report])
        ..contents[report.id] = content;
      final files = FakeAttachmentFiles();
      final viewModel = await _loaded(repository, files: files);

      final downloaded = await viewModel.download(report);

      expect(downloaded, isTrue);
      expect(files.saved, [('report.pdf', content, 'application/pdf')]);
      expect(viewModel.error, isNull);
    });

    test('names the file when it cannot be fetched', () async {
      final report = attachment();
      final repository = FakeAttachmentRepository([report]);
      final files = FakeAttachmentFiles();
      final viewModel = await _loaded(repository, files: files);
      repository.failure = const ApiException('gone', statusCode: 404);

      final downloaded = await viewModel.download(report);

      expect(downloaded, isFalse);
      expect(files.saved, isEmpty);
      expect(viewModel.error, 'Could not download report.pdf. Try again.');
    });
  });

  group('delete', () {
    test('deletes the attachment and drops it from the list', () async {
      final report = attachment();
      final notes = attachment(id: 'att-2', fileName: 'notes.txt');
      final repository = FakeAttachmentRepository([report, notes]);
      final viewModel = await _loaded(repository);

      final deleted = await viewModel.delete(report);

      expect(deleted, isTrue);
      expect(repository.deleted, ['att-1']);
      expect(viewModel.attachments, [notes]);
    });

    test('keeps the attachment listed when the delete fails', () async {
      final report = attachment();
      final repository = FakeAttachmentRepository([report]);
      final viewModel = await _loaded(repository);
      repository.failure = const ApiException('boom', statusCode: 500);

      final deleted = await viewModel.delete(report);

      expect(deleted, isFalse);
      expect(viewModel.attachments, [report]);
      expect(viewModel.error, 'Could not delete report.pdf. Try again.');
    });
  });
}

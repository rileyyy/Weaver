import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';
import 'package:weaver/features/work_item_detail/work_item_attachments_view_model.dart';

import 'fakes/fake_attachment_files.dart';
import 'fakes/fake_attachment_repository.dart';

const _picked = AttachmentUpload(
  fileName: 'quote.pdf',
  bytes: [1, 2, 3],
  contentType: 'application/pdf',
);

Future<WorkItemAttachmentsViewModel> _loaded(
  FakeAttachmentRepository repository, {
  FakeAttachmentFiles? files,
}) async {
  final viewModel = WorkItemAttachmentsViewModel(
    repository,
    files ?? FakeAttachmentFiles(),
  );
  await viewModel.load('item-1');
  return viewModel;
}

void main() {
  group('load', () {
    test("lists the item's attachments", () async {
      final viewModel = WorkItemAttachmentsViewModel(
        FakeAttachmentRepository([
          attachment(),
          attachment(id: 'att-2', fileName: 'notes.txt'),
        ]),
        FakeAttachmentFiles(),
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
      final viewModel = WorkItemAttachmentsViewModel(
        repository,
        FakeAttachmentFiles(),
      );

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

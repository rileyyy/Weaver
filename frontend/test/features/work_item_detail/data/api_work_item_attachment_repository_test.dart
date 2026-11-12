import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/work_item_detail/data/api_work_item_attachment_repository.dart';
import 'package:weaver/features/work_item_detail/models/attachment_upload.dart';

import '../../../core/network/fakes/fixed_api_endpoint.dart';

const _endpoint = FixedApiEndpoint('http://backend.test/api');

ApiWorkItemAttachmentRepository _repository(http.Client client) =>
    ApiWorkItemAttachmentRepository(client, _endpoint);

Map<String, dynamic> _attachmentJson({
  String id = 'att-1',
  String fileName = 'report.pdf',
  int sizeBytes = 2048,
}) => {
  'id': id,
  'workItemId': 'item-1',
  'fileName': fileName,
  'contentType': 'application/pdf',
  'sizeBytes': sizeBytes,
  'createdAtUtc': '2026-11-01T09:30:00Z',
};

void main() {
  test("loadAttachments reads the item's attachment list", () async {
    http.Request? sent;
    final client = MockClient((request) async {
      sent = request;
      return http.Response(
        jsonEncode([
          _attachmentJson(),
          _attachmentJson(id: 'att-2', fileName: 'notes.txt', sizeBytes: 12),
        ]),
        200,
      );
    });

    final attachments = await _repository(client).loadAttachments('item-1');

    expect(sent!.method, 'GET');
    expect(sent!.url.path, '/api/work-items/item-1/attachments');
    expect(attachments.map((a) => a.fileName), ['report.pdf', 'notes.txt']);
    final first = attachments.first;
    expect(first.id, 'att-1');
    expect(first.contentType, 'application/pdf');
    expect(first.sizeBytes, 2048);
    expect(first.createdAt, DateTime.utc(2026, 11, 1, 9, 30).toLocal());
  });

  test('upload posts the file as the multipart part named "file"', () async {
    http.Request? sent;
    final client = MockClient((request) async {
      sent = request;
      return http.Response(jsonEncode(_attachmentJson()), 201);
    });

    final attachment = await _repository(client).upload(
      'item-1',
      AttachmentUpload(
        fileName: 'report.pdf',
        bytes: utf8.encode('%PDF-1.7'),
        contentType: 'application/pdf',
      ),
    );

    expect(sent!.method, 'POST');
    expect(sent!.url.path, '/api/work-items/item-1/attachments');
    expect(sent!.headers['content-type'], startsWith('multipart/form-data'));
    final body = sent!.body;
    expect(body, contains('name="file"; filename="report.pdf"'));
    expect(body, contains('content-type: application/pdf'));
    expect(body, contains('%PDF-1.7'));
    expect(attachment.id, 'att-1');
  });

  test("a file over the limit fails with the server's message", () async {
    final client = MockClient(
      (_) async => http.Response(
        jsonEncode({
          'title': 'Payload Too Large',
          'status': 413,
          'detail': 'Attachments can be at most 25 MB.',
        }),
        413,
        headers: {'content-type': 'application/problem+json'},
      ),
    );

    final upload = _repository(client).upload(
      'item-1',
      AttachmentUpload(
        fileName: 'huge.bin',
        bytes: utf8.encode('x'),
        contentType: 'application/octet-stream',
      ),
    );

    await expectLater(
      upload,
      throwsA(
        isA<ApiException>()
            .having((e) => e.statusCode, 'statusCode', 413)
            .having(
              (e) => e.message,
              'message',
              contains('Attachments can be at most 25 MB.'),
            ),
      ),
    );
  });

  test('downloadContent returns the raw bytes, unchanged', () async {
    // Not valid UTF-8, so this fails if the body is decoded as text.
    final content = [0x89, 0x50, 0x4e, 0x47, 0xff, 0x00, 0xfe];
    http.Request? sent;
    final client = MockClient((request) async {
      sent = request;
      return http.Response.bytes(
        content,
        200,
        headers: {
          'content-type': 'image/png',
          'content-disposition': 'attachment; filename=pic.png',
        },
      );
    });

    final bytes = await _repository(client).downloadContent('att-1');

    expect(sent!.method, 'GET');
    expect(sent!.url.path, '/api/work-item-attachments/att-1/content');
    expect(bytes, content);
  });

  test('downloadContent fails with the status when the server refuses', () {
    final client = MockClient((_) async => http.Response('', 404));

    expect(
      _repository(client).downloadContent('att-1'),
      throwsA(
        isA<ApiException>().having((e) => e.statusCode, 'statusCode', 404),
      ),
    );
  });

  test('downloadThumbnail returns the thumbnail bytes, unchanged', () async {
    final thumbnail = [0x52, 0x49, 0x46, 0x46, 0xff, 0x00, 0xfe];
    http.Request? sent;
    final client = MockClient((request) async {
      sent = request;
      return http.Response.bytes(
        thumbnail,
        200,
        headers: {'content-type': 'image/webp'},
      );
    });

    final bytes = await _repository(client).downloadThumbnail('att-1');

    expect(sent!.method, 'GET');
    expect(sent!.url.path, '/api/work-item-attachments/att-1/thumbnail');
    expect(bytes, thumbnail);
  });

  test('downloadThumbnail fails with the status when there is none', () {
    final client = MockClient((_) async => http.Response('', 404));

    expect(
      _repository(client).downloadThumbnail('att-1'),
      throwsA(
        isA<ApiException>().having((e) => e.statusCode, 'statusCode', 404),
      ),
    );
  });

  test('delete deletes the work-item-attachments endpoint', () async {
    http.Request? sent;
    final client = MockClient((request) async {
      sent = request;
      return http.Response('', 204);
    });

    await _repository(client).delete('att-1');

    expect(sent!.method, 'DELETE');
    expect(sent!.url.path, '/api/work-item-attachments/att-1');
  });
}

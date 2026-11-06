"""只读比较既有Commerce目录；前者后来作为运行目录，绝不能当作事故前不可变基线。"""
from __future__ import annotations

import hashlib
import json
import struct
from pathlib import Path

BEFORE = Path('E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-LIGHT-01/v1.2/editor-isolated-commerce')
AFTER = Path('E:/WORLDCREATOR/XingHaiHuiLang/研究/VOXEL-LIGHT-01/v1.2/isolation-incident-after')
OUT = Path(__file__).resolve().parent


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def canonical_digest(value) -> str:
    return digest(json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':')).encode('utf-8'))


class Reader:
    def __init__(self, data: bytes):
        self.data = data
        self.offset = 0

    def take(self, count: int) -> bytes:
        result = self.data[self.offset:self.offset + count]
        if len(result) != count:
            raise ValueError('截断二进制字段')
        self.offset += count
        return result

    def number(self, fmt: str):
        return struct.unpack(fmt, self.take(struct.calcsize(fmt)))[0]

    def text(self) -> str:
        # 与.NET BinaryWriter.Write(string)的7-bit长度UTF-8格式对应。
        length = 0
        shift = 0
        while True:
            byte = self.number('<B')
            length |= (byte & 0x7f) << shift
            if byte < 128:
                break
            shift += 7
            if shift >= 35:
                raise ValueError('非法字符串长度')
        return self.take(length).decode('utf-8')


def parse_file(path: Path):
    data = path.read_bytes()
    base = {'file': str(path), 'length': len(data), 'sha256': digest(data)}
    if not path.name.endswith(('.comsave', '.comsave.bak')):
        base['text'] = data.decode('utf-8')
        return base
    outer = Reader(data)
    if outer.take(4) != b'OCS1':
        raise ValueError('错误存档magic')
    body_length = outer.number('<i')
    body = outer.take(body_length)
    expected_checksum = outer.take(32)
    if outer.offset != len(data) or body_length != len(data) - 40:
        raise ValueError('错误封套长度')
    if hashlib.sha256(body).digest() != expected_checksum:
        raise ValueError('封套SHA256校验失败')
    reader = Reader(body)
    envelope = {'schema': reader.number('<i'), 'profileId': reader.text(), 'sessionId': reader.text(),
                'stateRevision': reader.number('<q'), 'saveSequence': reader.number('<q'),
                'payloadSchema': reader.number('<i'), 'definitionFingerprint': reader.text()}
    payload_length = reader.number('<i')
    payload_bytes = reader.take(payload_length)
    if reader.offset != len(body):
        raise ValueError('错误payload长度')
    payload = json.loads(payload_bytes)
    # sections中payload为嵌套JSON字符串，额外解码后逐字段比较，避免仅比较长字符串。
    for section in payload.get('sections', []):
        section['payload'] = json.loads(section['payload'])
    return {**base, 'envelopeChecksumValid': True, 'envelope': envelope, 'payload': payload,
            'payloadSemanticSha256': canonical_digest(payload)}


def differences(a, b, path='$'):
    result = []
    if type(a) is not type(b):
        # .NET JSON可在等值整数/浮点之间变化，语义值相等不算业务变化。
        if isinstance(a, (int, float)) and isinstance(b, (int, float)) and a == b:
            return result
        return [{'path': path, 'before': a, 'after': b, 'kind': 'type-or-value'}]
    if isinstance(a, dict):
        for key in sorted(set(a) | set(b)):
            child = path + '.' + key
            if key not in a:
                result.append({'path': child, 'before': None, 'after': b[key], 'kind': 'added'})
            elif key not in b:
                result.append({'path': child, 'before': a[key], 'after': None, 'kind': 'removed'})
            else:
                result.extend(differences(a[key], b[key], child))
    elif isinstance(a, list):
        if len(a) != len(b):
            return [{'path': path, 'before': a, 'after': b, 'kind': 'list-length'}]
        for index, (old, new) in enumerate(zip(a, b)):
            result.extend(differences(old, new, f'{path}[{index}]'))
    elif a != b:
        item = {'path': path, 'before': a, 'after': b, 'kind': 'value'}
        if isinstance(a, (int, float)) and not isinstance(a, bool):
            item['delta'] = b - a
        result.append(item)
    return result


def analyze():
    old_files = {p.name: parse_file(p) for p in sorted(BEFORE.iterdir()) if p.is_file()}
    new_files = {p.name: parse_file(p) for p in sorted(AFTER.iterdir()) if p.is_file()}
    rows = []
    for name in sorted(set(old_files) | set(new_files)):
        old, new = old_files.get(name), new_files.get(name)
        row = {'name': name, 'beforeExists': old is not None, 'afterExists': new is not None}
        if old is None or new is None:
            rows.append(row)
            continue
        row.update(beforeSha256=old['sha256'], afterSha256=new['sha256'], bytesEqual=old['sha256'] == new['sha256'],
                   beforeLength=old['length'], afterLength=new['length'])
        if 'payload' in old:
            row.update(envelopeChecksumsValid=old['envelopeChecksumValid'] and new['envelopeChecksumValid'],
                       beforeEnvelope=old['envelope'], afterEnvelope=new['envelope'],
                       envelopeDifferences=differences(old['envelope'], new['envelope']),
                       payloadDifferences=differences(old['payload'], new['payload']),
                       beforePayloadSemanticSha256=old['payloadSemanticSha256'],
                       afterPayloadSemanticSha256=new['payloadSemanticSha256'])
        else:
            row['textDifferences'] = differences(old['text'], new['text'])
        rows.append(row)
    report = {'scope': '仅对既有目录读取；只输出审计，不恢复、不覆盖、不删除存档，不操作Unity。',
              'incidentBeforeBaselineAvailable': False,
              'baselineLimit': 'editor-isolated-commerce已作为后续隔离Play写入目录，其内容持续变化，不能作为事故前基线。下列before/after仅为本次读取的两个目录标签，不能据此证明事故只改变时长或未影响业务。首次约30秒原存档checkpoint已经变化，现只有p0初始hash、没有不可变的初始内容。',
              'restoreOrRollbackPerformed': False,
              'futureOriginalProtectionBaseline': str(AFTER),
              'beforeDirectory': str(BEFORE), 'afterDirectory': str(AFTER),
              'sourceContract': 'CommerceFileSaveStore.cs Encode/Decode：OCS1 + bodyLength + BinaryWriter字段 + SHA256(body)',
              'files': rows}
    target = OUT / 'commerce-isolation-incident-semantic-audit.json'
    target.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(str(target))
    for row in rows:
        print(row['name'], 'bytesEqual=', row.get('bytesEqual'))
        for diff in row.get('envelopeDifferences', []):
            print('ENVELOPE', json.dumps(diff, ensure_ascii=False))
        for diff in row.get('payloadDifferences', []):
            print('PAYLOAD', json.dumps(diff, ensure_ascii=False))
        for diff in row.get('textDifferences', []):
            print('TEXT', json.dumps(diff, ensure_ascii=False))
    return report


if __name__ == '__main__':
    import sys
    if '--annotate-existing' in sys.argv:
        # 仅给已产生的固定审计补充已知限制，避免重新采集正在变化的隔离运行目录。
        target = OUT / 'commerce-isolation-incident-semantic-audit.json'
        report = json.loads(target.read_text(encoding='utf-8'))
        report.update(incidentBeforeBaselineAvailable=False, restoreOrRollbackPerformed=False,
                      baselineLimit='editor-isolated-commerce后续已用作隔离Play写入目录，不能作为事故前不可变基线；下列before/after只是本次采样目录标签。首次约30秒原存档checkpoint已变化。p0仅存初始hash，没有初始内容，因此不能证明事故只改变时长、也不能证明库存/世界进度未变。当前采样差值不归因于事故。',
                      futureOriginalProtectionBaseline=str(AFTER))
        target.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
        print(str(target))
    else:
        analyze()

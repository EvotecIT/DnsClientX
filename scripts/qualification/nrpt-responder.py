"""Disposable runner-only UDP authority for native Windows NRPT routing proof."""
import socket, struct, json

with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as server:
    server.bind(('127.0.0.1', 53))
    print('ready', flush=True)
    while True:
        query, peer = server.recvfrom(65535)
        end, labels = 12, []
        while query[end]:
            length = query[end]; end += 1
            labels.append(query[end:end + length].decode('ascii')); end += length
        end += 1
        name = '.'.join(labels)
        kind, dns_class = struct.unpack('!HH', query[end:end + 4])
        if name != 'host.qualification.invalid' or kind != 1 or dns_class != 1:
            raise RuntimeError('Unexpected qualification query')
        answer = b'\xc0\x0c' + struct.pack('!HHIH', 1, 1, 60, 4) + socket.inet_aton('192.0.2.99')
        reply = query[:2] + struct.pack('!HHHHH', 0x8180, 1, 1, 0, 0) + query[12:end + 4] + answer
        server.sendto(reply, peer)
        print(json.dumps({'name': name, 'peer': peer, 'answer': '192.0.2.99'}), flush=True)

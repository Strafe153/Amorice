# Amorice

A primitive learning-oriented application-layer protocol built on top of [TCP](https://en.wikipedia.org/wiki/Transmission_Control_Protocol).

Amorice uses fixed-size 4096-byte packets to transmit data and provides per-packet acknowledgments. Its implementation aims to minimize allocations by taking advantage of stack-allocated `Span<byte>` and reusing byte buffers where possible.

## Disclaimer

Even though the program aims to be useful, it is distributed under the MIT License WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND.  
The users are solely responsible for determining the appropriateness of using or redistributing the Work and assume any risks associated with their exercise of permissions under the License.

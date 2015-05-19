#define GRAPH
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using E2D2.Collections.Concurrent;
using System.Runtime.CompilerServices; 

namespace E2D2 {
public class Packet {
  Int64 id_;
  public Int64 id 
  {
      get {return id_;}
  }
  public Packet(Int64 id) {
    id_ = id;
  }
}

public sealed class RingThroughputTestNoAllocate {

  private LLRing<Packet> queue_;

  private int producerCore_;
  private int consumerCore_;
  public long received_;
  public long produceBatchSize_;
  public long receiveBatchSize_;
  public long seconds_;
  public long warm_;
  public uint ringSize_;
  private bool producerStop_;
  public RingThroughputTestNoAllocate(int producerCore, 
                                 int consumerCore, 
                                 uint ringSize,
                                 int produceBatchSize, 
                                 int receiveBatch,
                                 long measureTime,
                                 long warmTime) {
    ringSize_ = ringSize;
    queue_ = new LLRing<Packet>(ringSize, false, false);
    producerCore_ = producerCore;
    consumerCore_ = consumerCore;
    received_ = 0;
    produceBatchSize_ = produceBatchSize;
    receiveBatchSize_ = receiveBatch;
    seconds_ = measureTime;
    warm_ = warmTime;
    producerStop_ = false;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void ProducerStart() {
    SysUtils.SetAffinity(producerCore_);
    Stopwatch stopwatch = new Stopwatch();
    stopwatch.Start();
    long absCount = 0;
    Packet[] batch = new Packet[produceBatchSize_]; 
    for (int i = 0; i < batch.Length; i++) {
        batch[i] = new Packet(absCount);
        absCount++;
    }
    while (!producerStop_) {
      queue_.MultiProducerEnqueue(ref batch);
    }
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void ConsumerStart() {
    SysUtils.SetAffinity(consumerCore_);
    Stopwatch stopwatch = new Stopwatch();
    stopwatch.Start();
    long lastSec = SysUtils.GetSecond(stopwatch);
    long lastElapsed = stopwatch.ElapsedMilliseconds;
    long count = 0;
    long seconds = 0;
    Packet[] batch = new Packet[receiveBatchSize_];
    long batchSize = receiveBatchSize_;
    while (SysUtils.GetSecond(stopwatch) - lastSec < warm_) {
        queue_.MultiConsumerDequeue(ref batch);
    }
    seconds = 0;
    lastSec = SysUtils.GetSecond(stopwatch);
    lastElapsed = stopwatch.ElapsedMilliseconds;
    while (true) {
      int dequed = (int)queue_.MultiConsumerDequeue(ref batch);
      count += dequed;
      long currSec = SysUtils.GetSecond(stopwatch);
      if (currSec != lastSec) {
        seconds += (currSec - lastSec);
        lastSec = currSec;
        if (seconds >= seconds_) {
            long currElapsed = stopwatch.ElapsedMilliseconds;
            long elapsedSec = (currElapsed - lastElapsed) / 1000;
            Console.WriteLine(
                ringSize_ + " " + receiveBatchSize_ + " " +
                count/elapsedSec + " " 
                + elapsedSec);
            seconds = 0;
            count = 0;
            lastElapsed = currElapsed;
#if GRAPH
            return;
#endif
        }
      }
    }
  }

  public void Start() {
    Console.WriteLine("Starting");
    Thread producer = new Thread(new ThreadStart(this.ProducerStart));
    //Thread consumer = new Thread(new ThreadStart(this.ConsumerStart));
    producer.Start();
    Thread.Sleep(10000);
    Console.WriteLine("Done sleeping");
    //consumer.Start();
    //consumer.Join();
    producer.Abort();
//    producerStop_ = true;
    producer.Join();
    Console.WriteLine("Done");
  }
}
public class RingThroughputTest {
  public static void Main (string[] args) {
    const uint RING_SIZE = 2048;
    const int BUFFER = 32;
    const int WARM_TIME = 2;
    const int RUN_TIME = 5;
    const int PCORE = 1;
    const int CCORE = 3;
    #if __MonoCS__
    Console.WriteLine("Running Mono");
    #else
    Console.WriteLine("Running Windows");
    #endif
    //Test();
#if GRAPH
    RingThroughputTestNoAllocate jit = new RingThroughputTestNoAllocate(PCORE, CCORE, BUFFER, 2, 2, RUN_TIME, WARM_TIME);
    jit.Start();
#endif
    // Actual test
#if GRAPH
    //for (int bpow = 1; bpow < 11; bpow++)
    //{
        for (int batchp = 0; batchp < 14; batchp++ )
        {
            int buffer = (1 << batchp);
            uint slots = (1u << 14);
#else
            uint slots = (1u << 14);
            int buffer = BUFFER;
#endif
            RingThroughputTestNoAllocate rt = new RingThroughputTestNoAllocate(PCORE, CCORE, slots, buffer, buffer, RUN_TIME, WARM_TIME);
            rt.Start();
#if GRAPH
        }
    //}
#endif
  }
}
}

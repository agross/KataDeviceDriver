using System.Diagnostics;

using Xunit;

namespace DeviceDriver;

public class DeviceDriverTests
{
  [Fact]
  public void Can_read_from_hardware()
  {
    IFlashMemoryDevice flash = null;
    var driver = new DeviceDriver(flash);

    var byteRead = driver.Read(0xFF);
    
    Assert.Equal(0, byteRead);
  }
  
  // More facts to write for you...
}

public interface IFlashMemoryDevice
{
  byte Read(long address);

  void Write(long address, byte data);
}

// This class is used by the operating system to interact with the hardware 'IFlashMemoryDevice'.
public class DeviceDriver
{
  static readonly long INIT_ADDRESS = 0x00;

  static readonly byte PROGRAM_COMMAND = 0x40;
  static readonly byte READY_MASK = 0x02;
  static readonly byte READY_NO_ERROR = 0x00;

  static readonly long TIMEOUT_THRESHOLD = 100_000_000;

  static readonly byte RESET_COMMAND = 0xFF;
  static readonly byte VPP_MASK = 0x20;
  static readonly byte INTERNAL_ERROR_MASK = 0x10;
  static readonly byte PROTECTED_BLOCK_ERROR_MASK = 0x08;

  readonly IFlashMemoryDevice _hardware;

  public DeviceDriver(IFlashMemoryDevice hardware)
  {
    _hardware = hardware;
  }

  public byte Read(long address) 
    => _hardware.Read(address);

  public void Write(long address, byte data)
  {
    var sw = new Stopwatch();
    sw.Start();
    
    _hardware.Write(INIT_ADDRESS, PROGRAM_COMMAND);
    _hardware.Write(address, data);
    
    byte readyByte;
    while (((readyByte = _hardware.Read(INIT_ADDRESS)) & READY_MASK) == 0)
    {
      if (readyByte != READY_NO_ERROR)
      {
        _hardware.Write(INIT_ADDRESS, RESET_COMMAND);
        if ((readyByte & VPP_MASK) > 0)
        {
          throw new VppException();
        }

        if ((readyByte & INTERNAL_ERROR_MASK) > 0)
        {
          throw new InternalErrorException();
        }

        if ((readyByte & PROTECTED_BLOCK_ERROR_MASK) > 0)
        {
          throw new ProtectedBlockException();
        }
      }

      sw.Stop();
      if (sw.ElapsedMilliseconds > TIMEOUT_THRESHOLD)
      {
        throw new TimeoutException("Timeout when trying to read data from memory");
      }
    }

    byte actual = _hardware.Read(address);
    if (data != actual)
    {
      throw new ReadFailureException("Failed to read data from memory");
    }
  }
}

public class ReadFailureException : Exception
{
  public string FailedToReadDataFromMemory { get; }

  public ReadFailureException(string failedToReadDataFromMemory)
  {
    FailedToReadDataFromMemory = failedToReadDataFromMemory;
  }
}

public class ProtectedBlockException : Exception
{
}

public class InternalErrorException : Exception
{
}

public class VppException : Exception
{
}

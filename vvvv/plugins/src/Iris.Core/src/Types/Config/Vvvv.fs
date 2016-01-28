namespace Iris.Core.Types.Config

open Iris.Core.Types

/// Models 
[<AutoOpen>]
[<ReflectedDefinition>]
module Vvvv =

  type VvvvExe =
    { Executable : FilePath
    ; Version    : Version
    ; Required   : bool 
    }

  type VvvvPlugin =
    { Name : Name
    ; Path : FilePath
    }

  type VvvvConfig =
    { Executables : VvvvExe list
    ; Plugins     : VvvvPlugin list
    }
    with
      static member Default =
        { Executables = List.empty
        ; Plugins     = List.empty
        }

  type PortConfig =
    { WebSocket : uint32
    ; UDPCue    : uint32
    ; Iris      : uint32
    }
    with
      static member Default =
        { WebSocket = 8080u
        ; UDPCue    = 8075u
        ; Iris      = 9090u
        }
        
  type TimingConfig =
    { Framebase : uint32
    ; Input     : string
    ; Servers   : IP list
    ; UDPPort   : uint32
    ; TCPPort   : uint32
    }
    with
      static member Default =
        { Framebase = 50u
        ; Input     = "Iris Freerun"
        ; Servers   = List.empty
        ; UDPPort   = 8071u
        ; TCPPort   = 8072u
        }

  type AudioConfig =
    { SampleRate : uint32 }
    with
      static member Default =
        { SampleRate = 48000u }

  type ViewPort =
    { Id             : Id
    ; Name           : Name
    ; Position       : Coordinate
    ; Size           : Rect
    ; OutputPosition : Coordinate
    ; OutputSize     : Rect
    ; Overlap        : Rect
    ; Description    : string
    }

  type Signal =
    { Size     : Rect
    ; Position : Coordinate
    }

  type Region =
    { Id             : Id
    ; Name           : Name
    ; SrcPosition    : Coordinate
    ; SrcSize        : Rect
    ; OutputPosition : Coordinate
    ; OutputSize     : Rect
    }

  type RegionMap =
    { SrcViewportId : Id
    ; Regions       : Region list
    }

  type Display =
    { Id        : Id
    ; Name      : Name
    ; Size      : Rect
    ; Signals   : Signal list
    ; RegionMap : RegionMap
    }

  type Argument = (string * string)

  type Task =
    { Id             : Id
    ; Description    : string
    ; DisplayId      : Id
    ; AudioStream    : string
    ; Arguments      : Argument list
    }

  type Node =
    { Id       : Id
    ; HostName : Name
    ; Ip       : IP
    ; Task     : Id
    }

  type HostGroup =
    { Name    : Name
    ; Members : Id list
    }

  type Cluster =
    { Name   : Name
    ; Nodes  : Node list
    ; Groups : HostGroup list
    }
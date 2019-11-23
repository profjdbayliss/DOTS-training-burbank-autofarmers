using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public struct Store : IComponentData
{
    public int moneyForFarmers;
    public int moneyForDrones;
    public int costOfFarmers;
    public int costOfDrones;
}

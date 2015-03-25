﻿namespace logic.core
open System
open System.Linq

module Logic = 

    // Type defs
    type VarName = string
    type Term = 
        | Var of VarName 
        | Pair of Term * Term 
        | Int of int 
        | Str of string
        | Empty


    type Subst = (VarName * Term) list
    type State = Subst * int
    type Goal<'T> = State -> seq<State * 'T>

    // walk : Term -> Subst -> Term
    let rec walk term subst =
        match term with
        | Var var -> 
            match List.tryFind (fun (var', _) -> var = var') subst with
            | Some (_, term') -> walk term' subst
            | None -> Var var
        | _ -> term

    let extend subst (var, term) = (var, term) :: subst

    // unify : Term -> Term -> Subst -> Subst option
    let rec unify term term' subst =
        match walk term subst, walk term' subst with
        | Var var, Var var' -> 
            if var = var' then 
                Some subst 
            else 
                Some <| extend subst (var, Var var') 
        | Var var, _ -> Some <| extend subst (var, term')
        | _, Var var -> Some <| extend subst (var, term)
        | Pair (leftTerm, rightTerm), Pair (leftTerm', rightTerm') ->
            match unify leftTerm leftTerm' subst with
            | Some subst' -> unify rightTerm rightTerm' subst'
            | None -> None
        | Int v, Int v' -> if v = v' then Some subst else None
        | Str v, Str v' -> if v = v' then Some subst else None 
        | _, _ -> None


    // disj : Goal<'T> -> Goal<'T> -> Goal<'T> 
    let disj (goal : Goal<'T>) (goal' : Goal<'T>) =
        fun (state, counter) ->
            seq { yield! Seq.append (goal (state, counter)) (goal' (state, counter)) }

    // conde : Goal<'T> list -> Goal<'T>
    let conde list = List.reduce disj list

    // (==) : Term -> Term -> Goal<unit>
    let (==) (term : Term) (term' : Term) : Goal<unit> = 
        fun (subst, counter) ->
            match unify term term' subst with
            | Some subst' -> seq { yield (subst', counter), () }
            | None -> Seq.empty

    // fresh : Goal<Term>
    let fresh : Goal<Term> = fun (subst, counter) -> seq { yield ((subst, counter + 1), (Var (sprintf "var%d" counter))) }

    // run : int -> (Term -> Goal<'T>) -> Term list
    let run n (f : Term -> Goal<'T>) = 
        let rec substVars subst term = 
            match term with
            | Var _ -> 
                let term' = walk term subst
                if term = term' then term
                else substVars subst term'
            | Pair (left, right) -> Pair (substVars subst left, substVars subst right)
            | Int _ -> term
            | Str _ -> term
            | Empty -> term  
        let goalVar = Var "__goal__"
        let seq = 
            ([], 0)
            |> f goalVar
        seq.Take(n)
        |> Seq.map (fun ((subst, _), _) -> substVars subst (walk goalVar subst))
        |> Seq.toList


    type LogicBuilder() =

        member this.Zero() = fun _ -> Seq.empty 
        member this.Return v = fun (subst, counter) -> seq { yield ((subst, counter), v) } 
        member this.ReturnFrom goal = goal 
    
        member this.Bind (goal : Goal<'T>, f : 'T -> Goal<'R>) : Goal<'R> =
            fun (subst, counter) -> 
                (subst, counter)
                |> goal
                |> Seq.collect (fun ((subst', counter'), v) -> 
                                    f v (subst', counter'))  

        member this.Delay (f : unit -> Goal<'T>) : Goal<'T> =
            fun (subst, counter) ->
                f () (subst, counter) 
        
    let logic = new LogicBuilder()


    